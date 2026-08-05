using Fruitables.Constants;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Infrastructure;

namespace Fruitables.Services.Identity.Authentication;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsService _settingsService;

    public GoogleAuthService(IUnitOfWork unitOfWork, ISettingsService settingsService)
    {
        _unitOfWork = unitOfWork;
        _settingsService = settingsService;
    }

    // Kiểm tra toggle IsEnabled từ DB (admin bật/tắt qua Settings page)
    public async Task<bool> IsGoogleAuthEnabledAsync()
    {
        var isEnabled = await _settingsService.GetSettingAsync(SettingKeys.GoogleAuthIsEnabled);
        return bool.TryParse(isEnabled, out var result) && result;
    }

    public async Task<GoogleAuthResult> ProcessGoogleLoginAsync(string email, string? name, string googleId)
    {
        if (string.IsNullOrWhiteSpace(email))
            return GoogleAuthResult.Failed(GoogleAuthErrorType.EmailNotProvided, "Không thể lấy email từ Google");

        var (user, lockReason) = await GetOrCreateUserFromGoogleAsync(email, name, googleId);

        if (user == null)
        {
            var errorMessage = !string.IsNullOrEmpty(lockReason)
                ? $"Tài khoản đã bị khóa. Lý do: {lockReason}"
                : "Tài khoản đã bị khóa";
            return GoogleAuthResult.Failed(GoogleAuthErrorType.AccountLocked, errorMessage);
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return GoogleAuthResult.Succeeded(user);
    }

    public async Task<(User? User, string? LockReason)> GetOrCreateUserFromGoogleAsync(string email, string? name, string googleId)
    {
        var normalizedEmail = email.ToLower();
        var existingUser = await _unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (existingUser != null)
        {
            // Tự động mở khóa khi khóa tạm thời đã hết hạn
            if (!existingUser.IsActive &&
                existingUser.CurrentLockType == LockType.Temporary &&
                existingUser.LockExpiresAt.HasValue &&
                existingUser.LockExpiresAt.Value <= DateTime.UtcNow)
            {
                existingUser.IsActive = true;
                existingUser.CurrentLockType = null;
                existingUser.LockReason = null;
                existingUser.LockViolationType = null;
                existingUser.LockedAt = null;
                existingUser.LockExpiresAt = null;
                existingUser.LockedByAdminId = null;
                await _unitOfWork.SaveChangesAsync();
            }

            if (!existingUser.IsActive)
                return (null, existingUser.LockReason);

            // Liên kết Google nếu chưa có
            if (string.IsNullOrEmpty(existingUser.GoogleId))
            {
                existingUser.GoogleId = googleId;
                existingUser.UpdatedAt = DateTime.UtcNow;
            }

            return (existingUser, null);
        }

        // Tạo user mới từ Google
        var newUser = new User
        {
            Email = normalizedEmail,
            Name = name ?? "Google User",
            Password = Guid.NewGuid().ToString(),
            GoogleId = googleId,
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(newUser);
        await _unitOfWork.SaveChangesAsync();

        return (newUser, null);
    }
}
