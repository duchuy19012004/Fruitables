using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Fruitables.Constants;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Chat.Knowledge;

namespace Fruitables.Services.Infrastructure;

// Quản lý cấu hình website, dữ liệu được cache 30 phút
public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IIndexingService _indexing;
    private readonly ILogger<SettingsService> _logger;

    private const string CacheKeyPrefix = "Setting_";
    private const string AllSettingsCacheKey = "AllSettings";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private const long MaxFileSizeBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".ico", ".svg" };
    private const string UploadsFolder = "uploads/settings";

    public SettingsService(
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        IWebHostEnvironment webHostEnvironment,
        IIndexingService indexing,
        ILogger<SettingsService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _webHostEnvironment = webHostEnvironment;
        _indexing = indexing;
        _logger = logger;
    }

    private async Task TryIndexAllowlistedSettingsIfNeededAsync(IEnumerable<string> keys)
    {
        if (!keys.Any(k => ChatSettingAllowlist.Keys.Contains(k)))
            return;

        try
        {
            await _indexing.IndexAllowlistedSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index allowlisted settings failed");
        }
    }

    public async Task<string?> GetSettingAsync(string key, string? defaultValue = null)
    {
        var cacheKey = $"{CacheKeyPrefix}{key}";

        if (_cache.TryGetValue(cacheKey, out string? cachedValue))
            return cachedValue ?? defaultValue;

        var setting = await _unitOfWork.Settings.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);

        var value = setting?.Value;
        _cache.Set(cacheKey, value, CacheDuration);

        return value ?? defaultValue;
    }

    public async Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default)
    {
        var stringValue = await GetSettingAsync(key);

        if (string.IsNullOrEmpty(stringValue))
            return defaultValue;

        try
        {
            var targetType = typeof(T);

            if (targetType == typeof(string))
                return (T)(object)stringValue;

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                if (int.TryParse(stringValue, out var intValue))
                    return (T)(object)intValue;
            }

            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                if (bool.TryParse(stringValue, out var boolValue))
                    return (T)(object)boolValue;
            }

            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            {
                if (decimal.TryParse(stringValue, out var decimalValue))
                    return (T)(object)decimalValue;
            }

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task<Dictionary<string, string?>> GetSettingsByGroupAsync(string group)
    {
        var settings = await _unitOfWork.Settings.Query()
            .AsNoTracking()
            .Where(s => s.Group == group)
            .ToListAsync();

        return settings.ToDictionary(s => s.Key, s => s.Value);
    }

    public async Task<List<Setting>> GetAllSettingsAsync()
    {
        if (_cache.TryGetValue(AllSettingsCacheKey, out List<Setting>? cachedSettings) && cachedSettings != null)
            return cachedSettings;

        var settings = await _unitOfWork.Settings.Query()
            .AsNoTracking()
            .ToListAsync();

        _cache.Set(AllSettingsCacheKey, settings, CacheDuration);
        return settings;
    }

    public async Task<SettingResult> SaveSettingAsync(string key, string? value, string? group = null)
    {
        try
        {
            if (key == SettingKeys.ContactEmail && !string.IsNullOrEmpty(value))
            {
                if (!ValidateEmail(value))
                    return SettingResult.Error("Email không đúng định dạng");
            }

            if (IsSocialUrlKey(key) && !string.IsNullOrEmpty(value))
            {
                if (!ValidateUrl(value))
                    return SettingResult.Error("URL không đúng định dạng");
            }

            var setting = await _unitOfWork.Settings.Query().FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new Setting { Key = key, Value = value, Group = group };
                await _unitOfWork.Settings.AddAsync(setting);
            }
            else
            {
                setting.Value = value;
                if (group != null) setting.Group = group;
            }

            await _unitOfWork.SaveChangesAsync();
            InvalidateCache(key);

            await TryIndexAllowlistedSettingsIfNeededAsync(new[] { key });

            return SettingResult.Ok(value);
        }
        catch (Exception ex)
        {
            return SettingResult.Error($"Lỗi khi lưu setting: {ex.Message}");
        }
    }

    public async Task<SettingResult> SaveSettingsAsync(Dictionary<string, string?> settings, string? group = null)
    {
        try
        {
            var keys = settings.Keys.ToList();
            var existingSettings = await _unitOfWork.Settings.Query()
                .Where(s => keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key);

            foreach (var kvp in settings)
            {
                if (!existingSettings.TryGetValue(kvp.Key, out var setting))
                {
                    setting = new Setting { Key = kvp.Key, Value = kvp.Value, Group = group };
                    await _unitOfWork.Settings.AddAsync(setting);
                }
                else
                {
                    setting.Value = kvp.Value;
                    if (group != null) setting.Group = group;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            foreach (var key in settings.Keys)
                InvalidateCache(key);

            await TryIndexAllowlistedSettingsIfNeededAsync(keys);

            return SettingResult.Ok();
        }
        catch (Exception ex)
        {
            return SettingResult.Error($"Lỗi khi lưu settings: {ex.Message}");
        }
    }

    public async Task<SettingResult> SaveFileSettingAsync(string key, IFormFile file, string? group = null)
    {
        try
        {
            if (file == null || file.Length == 0)
                return SettingResult.Error("File không được để trống");

            if (file.Length > MaxFileSizeBytes)
                return SettingResult.Error("File vượt quá 2MB");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!ValidateFileExtension(extension))
                return SettingResult.Error("Định dạng file không hợp lệ. Chỉ chấp nhận: jpg, png, gif, ico, svg");

            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, UploadsFolder);
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var uniqueFileName = $"{key}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var relativePath = $"/{UploadsFolder}/{uniqueFileName}";
            var result = await SaveSettingAsync(key, relativePath, group);

            if (!result.Success)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                return result;
            }

            return SettingResult.Ok(relativePath);
        }
        catch (Exception ex)
        {
            return SettingResult.Error($"Lỗi khi upload file: {ex.Message}");
        }
    }

    // Invalidate toàn bộ cache settings
    public void InvalidateCache()
    {
        _cache.Remove(AllSettingsCacheKey);
    }

    // Invalidate cache cho một key cụ thể
    public void InvalidateCache(string key)
    {
        _cache.Remove($"{CacheKeyPrefix}{key}");
        _cache.Remove(AllSettingsCacheKey);
    }

    #region Validation

    public static bool ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        const string emailPattern = @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$";
        try
        {
            return Regex.IsMatch(email, emailPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public static bool ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uriResult))
            return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;
        return false;
    }

    private static bool IsSocialUrlKey(string key)
    {
        return key == SettingKeys.SocialFacebook ||
               key == SettingKeys.SocialTwitter ||
               key == SettingKeys.SocialInstagram ||
               key == SettingKeys.SocialYoutube ||
               key == SettingKeys.SocialLinkedIn;
    }

    public static bool ValidateFileExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        return AllowedImageExtensions.Contains(extension.ToLowerInvariant());
    }

    public static bool ValidateFileSize(long fileSize)
    {
        return fileSize > 0 && fileSize <= MaxFileSizeBytes;
    }

    #endregion
}
