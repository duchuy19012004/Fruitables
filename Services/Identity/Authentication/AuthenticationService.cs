using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Identity.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuthenticationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failed("Email hoặc mật khẩu không được để trống");
        }

        // Find user by email
        var user = await GetUserByEmailAsync(email);
        if (user == null)
        {
            return AuthResult.Failed("Email hoặc mật khẩu không đúng");
        }

        // Check if temporary lock has expired and auto-unlock
        if (!user.IsActive && 
            user.CurrentLockType == LockType.Temporary && 
            user.LockExpiresAt.HasValue && 
            user.LockExpiresAt.Value <= DateTime.UtcNow)
        {
            // Auto-unlock expired temporary lock
            user.IsActive = true;
            user.CurrentLockType = null;
            user.LockReason = null;
            user.LockViolationType = null;
            user.LockedAt = null;
            user.LockExpiresAt = null;
            user.LockedByAdminId = null;
            await _unitOfWork.SaveChangesAsync();
        }

        // Check if account is active
        if (!user.IsActive)
        {
            var lockReason = !string.IsNullOrEmpty(user.LockReason) 
                ? $"Tài khoản đã bị khóa. Lý do: {user.LockReason}" 
                : "Tài khoản đã bị khóa";
            return AuthResult.Failed(lockReason);
        }

        // Check role - only Admin and SuperAdmin can login
        if (user.Role == UserRole.Customer)
        {
            return AuthResult.Failed("Bạn không có quyền truy cập");
        }

        // Verify password
        if (!VerifyPassword(password, user.Password))
        {
            return AuthResult.Failed("Email hoặc mật khẩu không đúng");
        }

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return AuthResult.Succeeded(user);
    }

    public Task LogoutAsync()
    {
        // Cookie logout will be handled by the controller
        return Task.CompletedTask;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Email == email);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
