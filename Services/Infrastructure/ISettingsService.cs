using Microsoft.AspNetCore.Http;
using Fruitables.Models;

namespace Fruitables.Services.Infrastructure;

/// <summary>
/// Interface cho Settings Service - quản lý cấu hình website
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Đọc giá trị setting theo key
    /// </summary>
    /// <param name="key">Key của setting</param>
    /// <param name="defaultValue">Giá trị mặc định nếu key không tồn tại</param>
    /// <returns>Giá trị setting hoặc defaultValue</returns>
    Task<string?> GetSettingAsync(string key, string? defaultValue = null);

    /// <summary>
    /// Đọc giá trị setting theo key với kiểu generic
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu cần convert</typeparam>
    /// <param name="key">Key của setting</param>
    /// <param name="defaultValue">Giá trị mặc định nếu key không tồn tại</param>
    /// <returns>Giá trị setting đã convert hoặc defaultValue</returns>
    Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Đọc tất cả settings theo group
    /// </summary>
    /// <param name="group">Tên group (General, SEO, Contact, Social)</param>
    /// <returns>Dictionary chứa key-value của các settings trong group</returns>
    Task<Dictionary<string, string?>> GetSettingsByGroupAsync(string group);

    /// <summary>
    /// Lấy tất cả settings
    /// </summary>
    /// <returns>Danh sách tất cả settings</returns>
    Task<List<Setting>> GetAllSettingsAsync();

    /// <summary>
    /// Lưu setting đơn lẻ
    /// </summary>
    /// <param name="key">Key của setting</param>
    /// <param name="value">Giá trị cần lưu</param>
    /// <param name="group">Group của setting (optional)</param>
    /// <returns>SettingResult cho biết thành công hay thất bại</returns>
    Task<SettingResult> SaveSettingAsync(string key, string? value, string? group = null);

    /// <summary>
    /// Lưu nhiều settings cùng lúc
    /// </summary>
    /// <param name="settings">Dictionary chứa key-value cần lưu</param>
    /// <param name="group">Group của các settings (optional)</param>
    /// <returns>SettingResult cho biết thành công hay thất bại</returns>
    Task<SettingResult> SaveSettingsAsync(Dictionary<string, string?> settings, string? group = null);

    /// <summary>
    /// Upload và lưu file setting (Logo, Favicon)
    /// </summary>
    /// <param name="key">Key của setting</param>
    /// <param name="file">File cần upload</param>
    /// <param name="group">Group của setting (optional)</param>
    /// <returns>SettingResult với đường dẫn file nếu thành công</returns>
    Task<SettingResult> SaveFileSettingAsync(string key, IFormFile file, string? group = null);

    /// <summary>
    /// Xóa toàn bộ cache settings
    /// </summary>
    void InvalidateCache();

    /// <summary>
    /// Xóa cache cho một key cụ thể
    /// </summary>
    /// <param name="key">Key cần xóa cache</param>
    void InvalidateCache(string key);
}
