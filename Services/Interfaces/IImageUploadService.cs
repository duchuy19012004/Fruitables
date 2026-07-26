using Microsoft.AspNetCore.Http;

namespace Fruitables.Services.Interfaces;

public interface IImageUploadService
{
    Task<string> UploadImageAsync(IFormFile file, string folder);
    Task<string> UploadProductImageAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task DeleteImageAsync(string imageUrl);
    bool IsValidImageFile(IFormFile file);
    bool IsValidFileSize(IFormFile file, long maxSizeBytes = 5 * 1024 * 1024);
}
