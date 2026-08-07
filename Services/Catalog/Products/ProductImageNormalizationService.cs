using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
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

    public ProductImageNormalizationService(
        ApplicationDbContext dbContext,
        IImageUploadService imageUploadService,
        IWebHostEnvironment environment,
        ILogger<ProductImageNormalizationService> logger)
    {
        _dbContext = dbContext;
        _imageUploadService = imageUploadService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ProductImageNormalizationResult> NormalizeAsync(
        bool apply,
        bool includeWebp = false,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var images = await _dbContext.ProductImages
            .Where(image => includeWebp || !image.ImageUrl.EndsWith(".webp"))
            .OrderBy(image => image.Id)
            .ToListAsync(cancellationToken);

        var result = new ProductImageNormalizationResult(images.Count);
        if (apply)
            result.BackupPath = await BackupAsync(images, cancellationToken);

        foreach (var image in images)
        {
            if (!TryResolveProductImagePath(image.ImageUrl, out var sourcePath) || !File.Exists(sourcePath))
            {
                result.Skipped++;
                _logger.LogWarning("Skipping missing or unsupported product image {ImageUrl}", image.ImageUrl);
                continue;
            }

            if (includeWebp && image.ImageUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
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

            var oldUrl = image.ImageUrl;
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

                image.ImageUrl = newUrl;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                result.Converted++;
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                image.ImageUrl = oldUrl;
                _dbContext.Entry(image).State = EntityState.Unchanged;
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

        foreach (var entry in manifest)
        {
            var image = await _dbContext.ProductImages.FindAsync([entry.Id], cancellationToken);
            if (image == null)
                continue;

            var currentUrl = image.ImageUrl;
            var backupFile = Path.Combine(resolvedBackupPath, entry.BackupFile);
            if (!TryResolveProductImagePath(entry.ImageUrl, out var targetFile))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            if (!File.Exists(targetFile))
                File.Copy(backupFile, targetFile);

            image.ImageUrl = entry.ImageUrl;
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (!string.Equals(currentUrl, entry.ImageUrl, StringComparison.OrdinalIgnoreCase))
                await _imageUploadService.DeleteImageAsync(currentUrl);
            restored++;
        }

        return restored;
    }

    private async Task<string> BackupAsync(
        IReadOnlyCollection<ProductImage> images,
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
            if (!TryResolveProductImagePath(image.ImageUrl, out var sourcePath) || !File.Exists(sourcePath))
                continue;

            var backupFile = $"{image.Id}-{Path.GetFileName(sourcePath)}";
            File.Copy(sourcePath, Path.Combine(backupPath, backupFile));
            manifest.Add(new { image.Id, image.ImageUrl, BackupFile = backupFile });
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

public sealed record ProductImageBackupEntry(int Id, string ImageUrl, string BackupFile);

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
