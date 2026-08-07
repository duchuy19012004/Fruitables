using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using System.Text.Json;

namespace Fruitables.Services.Catalog.Products;

public sealed class ProductImageNormalizationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IImageUploadService _imageUploadService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProductImageNormalizationService> _logger;
    private readonly IJsonDocumentSerializer _serializer;

    public ProductImageNormalizationService(
        ApplicationDbContext dbContext,
        IImageUploadService imageUploadService,
        IWebHostEnvironment environment,
        ILogger<ProductImageNormalizationService> logger,
        IJsonDocumentSerializer? serializer = null)
    {
        _dbContext = dbContext;
        _imageUploadService = imageUploadService;
        _environment = environment;
        _logger = logger;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    public async Task<ProductImageNormalizationResult> NormalizeAsync(
        bool apply,
        bool includeWebp = false,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var products = await _dbContext.Products.ToListAsync(cancellationToken);
        var images = products
            .SelectMany(product =>
            {
                var document = ProductAggregateJson.ReadImages(product.ImagesJson, _serializer);
                return document.Images
                    .Select((image, index) => new ProductImageNormalizationEntry(product, document, index, image));
            })
            .Where(image => includeWebp || !image.Document.Url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(image => image.Product.Id)
            .ThenBy(image => image.Document.SortOrder)
            .ToList();

        var result = new ProductImageNormalizationResult(images.Count);
        if (apply)
            result.BackupPath = await BackupAsync(images, cancellationToken);

        foreach (var image in images)
        {
            if (!TryResolveProductImagePath(image.Document.Url, out var sourcePath) || !File.Exists(sourcePath))
            {
                result.Skipped++;
                _logger.LogWarning("Skipping missing or unsupported product image {ImageUrl}", image.Document.Url);
                continue;
            }

            if (includeWebp && image.Document.Url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                var info = await Image.IdentifyAsync(sourcePath, cancellationToken);
                if (!force && info != null && Math.Max(info.Width, info.Height) >= 1000)
                {
                    result.Skipped++;
                    continue;
                }
            }

            if (!apply)
            {
                result.Eligible++;
                continue;
            }

            var oldUrl = image.Document.Url;
            string? newUrl = null;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var source = File.OpenRead(sourcePath))
                {
                    var formFile = new FormFile(source, 0, source.Length, "image", Path.GetFileName(sourcePath))
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = ContentTypeFor(sourcePath)
                    };

                    newUrl = await _imageUploadService.UploadProductImageAsync(formFile, cancellationToken);
                }

                var replacement = new ProductImageDocument
                {
                    Url = newUrl,
                    StorageKey = newUrl.TrimStart('/'),
                    IsPrimary = image.Document.IsPrimary,
                    SortOrder = image.Document.SortOrder
                };
                image.DocumentSet.Images[image.Index] = replacement;
                image.Product.ImagesJson = _serializer.Serialize(image.DocumentSet);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                result.Converted++;
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                image.DocumentSet.Images[image.Index] = image.Document;
                image.Product.ImagesJson = _serializer.Serialize(image.DocumentSet);
                _dbContext.Entry(image.Product).State = EntityState.Unchanged;
                if (newUrl != null)
                    await _imageUploadService.DeleteImageAsync(newUrl);

                result.Failed++;
                _logger.LogError(exception, "Could not normalize product image {ImageUrl}", oldUrl);
                continue;
            }

            // Keep the source file so the migration remains repeatable in deployments
            // where the seeded product images are version-controlled assets.
        }

        return result;
    }

    public async Task<int> RollbackAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        var backupRoot = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "ProductImageBackups"));
        var resolvedBackupPath = Path.GetFullPath(backupPath);
        if (!resolvedBackupPath.StartsWith(backupRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup path is outside App_Data/ProductImageBackups");

        var manifestPath = Path.Combine(resolvedBackupPath, "manifest.json");
        var manifest = JsonSerializer.Deserialize<List<ProductImageBackupEntry>>(
            await File.ReadAllTextAsync(manifestPath, cancellationToken)) ?? [];
        var restored = 0;

        var products = await _dbContext.Products.ToListAsync(cancellationToken);

        foreach (var entry in manifest)
        {
            var product = products.FirstOrDefault(candidate => candidate.Id == entry.ProductId);
            if (product == null)
                continue;

            var document = ProductAggregateJson.ReadImages(product.ImagesJson, _serializer);
            var imageIndex = entry.Id - 1;
            if (imageIndex < 0 || imageIndex >= document.Images.Count)
                continue;

            var currentUrl = document.Images[imageIndex].Url;
            var backupFile = Path.Combine(resolvedBackupPath, entry.BackupFile);
            if (!TryResolveProductImagePath(entry.ImageUrl, out var targetFile))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            if (!File.Exists(targetFile))
                File.Copy(backupFile, targetFile);

            var restoredImage = new ProductImageDocument
            {
                Url = entry.ImageUrl,
                StorageKey = entry.ImageUrl.TrimStart('/'),
                IsPrimary = document.Images[imageIndex].IsPrimary,
                SortOrder = document.Images[imageIndex].SortOrder
            };
            var updatedImages = document.Images.ToList();
            updatedImages[imageIndex] = restoredImage;
            product.ImagesJson = _serializer.Serialize(new ProductImagesDocument { Images = updatedImages });
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (!string.Equals(currentUrl, entry.ImageUrl, StringComparison.OrdinalIgnoreCase))
                await _imageUploadService.DeleteImageAsync(currentUrl);
            restored++;
        }

        return restored;
    }

    private async Task<string> BackupAsync(
        IReadOnlyCollection<ProductImageNormalizationEntry> images,
        CancellationToken cancellationToken)
    {
        var backupPath = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "ProductImageBackups",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupPath);

        var manifest = new List<object>();
        foreach (var image in images)
        {
            if (!TryResolveProductImagePath(image.Document.Url, out var sourcePath) || !File.Exists(sourcePath))
                continue;

            var backupFile = $"{image.Product.Id}-{image.Id}-{Path.GetFileName(sourcePath)}";
            File.Copy(sourcePath, Path.Combine(backupPath, backupFile));
            manifest.Add(new ProductImageBackupEntry(
                image.Product.Id,
                image.Id,
                image.Document.Url,
                backupFile));
        }

        await File.WriteAllTextAsync(
            Path.Combine(backupPath, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        return backupPath;
    }

    private bool TryResolveProductImagePath(string imageUrl, out string path)
    {
        path = string.Empty;
        if (!imageUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
            return false;

        var webRoot = Path.GetFullPath(_environment.WebRootPath);
        var candidate = Path.GetFullPath(Path.Combine(
            webRoot,
            imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;

        path = candidate;
        return true;
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}

public sealed record ProductImageBackupEntry(int ProductId, int Id, string ImageUrl, string BackupFile);

internal sealed record ProductImageNormalizationEntry(
    Product Product,
    ProductImagesDocument DocumentSet,
    int Index,
    ProductImageDocument Document)
{
    public int Id => Index + 1;
}

public sealed class ProductImageNormalizationResult
{
    public ProductImageNormalizationResult(int discovered) => Discovered = discovered;

    public int Discovered { get; }
    public int Eligible { get; set; }
    public int Converted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string? BackupPath { get; set; }
}
