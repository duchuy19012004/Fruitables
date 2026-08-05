using Microsoft.AspNetCore.Http;
using Fruitables.ViewModels;

namespace Fruitables.Services.Identity.Profiles;

/// <summary>
/// Interface cho ProfileService - quản lý thông tin profile người dùng
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Lấy thông tin profile của user
    /// </summary>
    /// <param name="userId">ID của user</param>
    /// <returns>ProfileResult chứa thông tin profile hoặc lỗi</returns>
    Task<ProfileResult> GetProfileAsync(int userId);
    
    /// <summary>
    /// Cập nhật thông tin profile (tên, số điện thoại)
    /// </summary>
    /// <param name="request">Request chứa thông tin cần cập nhật</param>
    /// <returns>ProfileResult với thông tin đã cập nhật hoặc lỗi</returns>
    Task<ProfileResult> UpdateProfileAsync(UpdateProfileRequest request);
    
    /// <summary>
    /// Upload avatar mới cho user
    /// </summary>
    /// <param name="userId">ID của user</param>
    /// <param name="file">File ảnh avatar</param>
    /// <returns>ProfileResult với avatar URL mới hoặc lỗi</returns>
    Task<ProfileResult> UpdateAvatarAsync(int userId, IFormFile file);
    
    /// <summary>
    /// Xóa avatar của user và đặt về mặc định
    /// </summary>
    /// <param name="userId">ID của user</param>
    /// <returns>ProfileResult với trạng thái xóa</returns>
    Task<ProfileResult> DeleteAvatarAsync(int userId);
}
