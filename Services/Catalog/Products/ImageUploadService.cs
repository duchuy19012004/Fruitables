using Fruitables.Services.Communications;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Fruitables.Services.Catalog.Products;

public class ImageUploadService : IImageUploadService
{
    private const int ProductImageSize = 1000;
    private const long MaxProductPixels = 20_000_000;
    private readonly IWebHostEnvironment _environment;
    private static readonly string[] ValidImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] ValidImageContentTypes = 
    { 
        "image/jpeg", 
        "image/jpg", 
        "image/png", 
        "image/gif", 
        "image/webp" 
    };

    public ImageUploadService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder)
    {
        if (!IsValidImageFile(file))
            throw new InvalidOperationException("File không phải định dạng ảnh hợp lệ");

        if (!IsValidFileSize(file))
            throw new InvalidOperationException("File vượt quá kích thước cho phép (5MB)");

        // Create upload directory if not exists
        var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", folder);
        if (!Directory.Exists(uploadPath))
        {
            Directory.CreateDirectory(uploadPath);
        }

        // Generate unique filename
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadPath, fileName);

        // Save file
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Return relative URL
        return $"/uploads/{folder}/{fileName}";
    }

    public async Task<string> UploadProductImageAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidImageFile(file))
            throw new InvalidOperationException("File không phải định dạng ảnh hợp lệ");

        if (!IsValidFileSize(file))
            throw new InvalidOperationException("File vượt quá kích thước cho phép (5MB)");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension == ".gif")
            throw new InvalidOperationException("Ảnh GIF không được hỗ trợ cho sản phẩm");

        try
        {
            await using (var identifyStream = file.OpenReadStream())
            {
                var info = await Image.IdentifyAsync(identifyStream, cancellationToken);
                if (info == null || (long)info.Width * info.Height > MaxProductPixels)
                    throw new InvalidOperationException("Ảnh vượt quá giới hạn 20 megapixel");
            }

            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync<Rgba32>(input, cancellationToken);

            image.Mutate(context => context
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(ProductImageSize, ProductImageSize),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));
            WhitenConnectedEdgeBackground(image);

            var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadPath);

            var fileName = $"{Guid.NewGuid():N}.webp";
            var filePath = Path.Combine(uploadPath, fileName);
            var temporaryPath = filePath + ".tmp";

            try
            {
                await image.SaveAsWebpAsync(
                    temporaryPath,
                    new WebpEncoder { Quality = 82 },
                    cancellationToken);
                File.Move(temporaryPath, filePath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            return $"/uploads/products/{fileName}";
        }
        catch (UnknownImageFormatException)
        {
            throw new InvalidOperationException("Nội dung file không phải ảnh hợp lệ");
        }
        catch (InvalidImageContentException)
        {
            throw new InvalidOperationException("File ảnh bị hỏng hoặc không thể đọc");
        }
    }

    public Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.CompletedTask;

        // Convert URL to physical path
        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var webRoot = Path.GetFullPath(_environment.WebRootPath);
        var filePath = Path.GetFullPath(Path.Combine(webRoot, relativePath));

        if (!filePath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        // Delete file if exists
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public bool IsValidImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return false;

        // Check extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ValidImageExtensions.Contains(extension))
            return false;

        // Check content type
        if (!ValidImageContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return false;

        return true;
    }

    public bool IsValidFileSize(IFormFile file, long maxSizeBytes = 5 * 1024 * 1024)
    {
        if (file == null)
            return false;

        return file.Length <= maxSizeBytes;
    }

    private static void WhitenConnectedEdgeBackground(Image<Rgba32> image)
    {
        var visited = new bool[image.Width * image.Height];
        var queue = new Queue<Point>();

        void Enqueue(int x, int y)
        {
            var index = y * image.Width + x;
            if (visited[index] || !IsNearWhite(image[x, y]))
                return;

            visited[index] = true;
            queue.Enqueue(new Point(x, y));
        }

        for (var x = 0; x < image.Width; x++)
        {
            Enqueue(x, 0);
            Enqueue(x, image.Height - 1);
        }

        for (var y = 1; y < image.Height - 1; y++)
        {
            Enqueue(0, y);
            Enqueue(image.Width - 1, y);
        }

        while (queue.TryDequeue(out var point))
        {
            image[point.X, point.Y] = Color.White;
            if (point.X > 0) Enqueue(point.X - 1, point.Y);
            if (point.X + 1 < image.Width) Enqueue(point.X + 1, point.Y);
            if (point.Y > 0) Enqueue(point.X, point.Y - 1);
            if (point.Y + 1 < image.Height) Enqueue(point.X, point.Y + 1);
        }
    }

    private static bool IsNearWhite(Rgba32 pixel)
    {
        var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        return pixel.A > 240 && minimum >= 235 && maximum - minimum <= 16;
    }
}
