using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Fruitables.Data;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Catalog.Products;

namespace Fruitables.Services.Identity.Profiles;

/// <summary>
/// Service quản lý thông tin profile người dùng
/// </summary>
public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageUploadService _imageUploadService;
    private const string DefaultAvatarUrl = "/img/default-avatar.svg";
    private const string AvatarFolder = "avatars";
    private const long MaxAvatarSize = 2 * 1024 * 1024; // 2MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };

    public ProfileService(ApplicationDbContext context, IImageUploadService imageUploadService)
    {
        _context = context;
        _imageUploadService = imageUploadService;
    }

    /// <inheritdoc />
    public async Task<ProfileResult> GetProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        
        if (user == null)
        {
            return ProfileResult.Fail(ProfileErrorType.NotFound, "Không tìm thấy người dùng");
        }

        var profile = new ProfileViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            AvatarUrl = string.IsNullOrEmpty(user.Avatar) ? DefaultAvatarUrl : user.Avatar,
            CreatedAt = user.CreatedAt
        };

        return ProfileResult.Ok(profile);
    }

    /// <inheritdoc />
    public async Task<ProfileResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        // Validate name
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ProfileResult.Fail(ProfileErrorType.ValidationError, "Tên không được để trống");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length < 1 || trimmedName.Length > 200)
        {
            return ProfileResult.Fail(ProfileErrorType.ValidationError, "Tên phải từ 1-200 ký tự");
        }

        // Validate phone (optional but if provided must be valid)
        if (!string.IsNullOrEmpty(request.Phone))
        {
            var trimmedPhone = request.Phone.Trim();
            if (!IsValidPhone(trimmedPhone))
            {
                return ProfileResult.Fail(ProfileErrorType.ValidationError, "Số điện thoại không hợp lệ");
            }
        }

        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
        {
            return ProfileResult.Fail(ProfileErrorType.NotFound, "Không tìm thấy người dùng");
        }

        user.Name = trimmedName;
        user.Phone = string.IsNullOrEmpty(request.Phone) ? null : request.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetProfileAsync(request.UserId);
    }

    /// <inheritdoc />
    public async Task<ProfileResult> UpdateAvatarAsync(int userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return ProfileResult.Fail(ProfileErrorType.ValidationError, "Vui lòng chọn file ảnh");
        }

        // Validate file type
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return ProfileResult.Fail(ProfileErrorType.InvalidFileType, "Chỉ chấp nhận file ảnh (jpg, jpeg, png, gif)");
        }

        // Validate file size
        if (file.Length > MaxAvatarSize)
        {
            return ProfileResult.Fail(ProfileErrorType.FileTooLarge, "File không được vượt quá 2MB");
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return ProfileResult.Fail(ProfileErrorType.NotFound, "Không tìm thấy người dùng");
        }

        try
        {
            // Delete old avatar if exists
            if (!string.IsNullOrEmpty(user.Avatar) && user.Avatar != DefaultAvatarUrl)
            {
                await _imageUploadService.DeleteImageAsync(user.Avatar);
            }

            // Upload new avatar
            var avatarUrl = await _imageUploadService.UploadImageAsync(file, AvatarFolder);
            
            user.Avatar = avatarUrl;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetProfileAsync(userId);
        }
        catch (Exception)
        {
            return ProfileResult.Fail(ProfileErrorType.UploadFailed, "Không thể upload file. Vui lòng thử lại");
        }
    }

    /// <inheritdoc />
    public async Task<ProfileResult> DeleteAvatarAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return ProfileResult.Fail(ProfileErrorType.NotFound, "Không tìm thấy người dùng");
        }

        // Delete avatar file if exists
        if (!string.IsNullOrEmpty(user.Avatar) && user.Avatar != DefaultAvatarUrl)
        {
            await _imageUploadService.DeleteImageAsync(user.Avatar);
        }

        user.Avatar = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetProfileAsync(userId);
    }

    /// <summary>
    /// Kiểm tra số điện thoại hợp lệ (chỉ chứa số, độ dài 10-20)
    /// </summary>
    private static bool IsValidPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone))
            return true; // Phone is optional

        if (phone.Length < 10 || phone.Length > 20)
            return false;

        return phone.All(char.IsDigit);
    }
}
