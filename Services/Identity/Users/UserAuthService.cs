using Fruitables.Data;
using Fruitables.Models;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Communications;

namespace Fruitables.Services.Identity.Users;

/// <summary>
/// Service for user authentication (registration, login, logout)
/// </summary>
public class UserAuthService : IUserAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;

    public UserAuthService(ApplicationDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task<RegistrationResult> RegisterAsync(RegisterRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return RegistrationResult.Failed(
                RegistrationErrorType.ValidationError,
                "Vui lòng điền đầy đủ thông tin");
        }

        // Validate email format
        if (!IsValidEmail(request.Email))
        {
            return RegistrationResult.Failed(
                RegistrationErrorType.InvalidEmail,
                "Email không đúng định dạng");
        }

        // Check password length
        if (request.Password.Length < 6)
        {
            return RegistrationResult.Failed(
                RegistrationErrorType.PasswordTooShort,
                "Mật khẩu phải có ít nhất 6 ký tự");
        }

        // Check password match
        if (request.Password != request.ConfirmPassword)
        {
            return RegistrationResult.Failed(
                RegistrationErrorType.PasswordMismatch,
                "Mật khẩu xác nhận không khớp");
        }

        // Check duplicate email
        var existingUser = await GetUserByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return RegistrationResult.Failed(
                RegistrationErrorType.EmailAlreadyExists,
                "Email đã được sử dụng");
        }

        // Create new user
        var user = new User
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLower(),
            Password = HashPassword(request.Password),
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        return RegistrationResult.Succeeded(user);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failed("Email hoặc mật khẩu không được để trống");
        }

        // Find user by email
        var user = await GetUserByEmailAsync(email.Trim().ToLower());
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
            await _db.SaveChangesAsync();
        }

        // Check if account is active
        if (!user.IsActive)
        {
            var lockReason = !string.IsNullOrEmpty(user.LockReason) 
                ? $"Tài khoản đã bị khóa. Lý do: {user.LockReason}" 
                : "Tài khoản đã bị khóa";
            return AuthResult.Failed(lockReason);
        }

        // Verify password
        if (!VerifyPassword(password, user.Password))
        {
            return AuthResult.Failed("Email hoặc mật khẩu không đúng");
        }

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return AuthResult.Succeeded(user);
    }

    public Task LogoutAsync()
    {
        // Cookie logout will be handled by the controller
        return Task.CompletedTask;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public string GetRedirectUrlByRole(UserRole role)
    {
        return role switch
        {
            UserRole.Customer => "/Home/Index",
            UserRole.Admin => "/Admin/Dashboard",
            UserRole.SuperAdmin => "/Admin/Dashboard",
            _ => "/Home/Index"
        };
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

    /// <inheritdoc />
    public async Task<bool> GeneratePasswordResetTokenAsync(string email, string resetCallbackUrl)
    {
        // Always return true for security (don't reveal whether email exists)
        var user = await GetUserByEmailAsync(email.Trim().ToLower());
        if (user == null)
            return true;

        // Generate a secure random token
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        // Save token & expiry (15 minutes) to DB
        user.ResetPasswordToken = token;
        user.ResetPasswordTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Build reset link and send email
        var resetLink = $"{resetCallbackUrl}?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";
        await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
            return false;

        var user = await GetUserByEmailAsync(request.Email.Trim().ToLower());
        if (user == null)
            return false;

        // Validate token
        if (user.ResetPasswordToken != request.Token)
            return false;

        // Validate expiry
        if (user.ResetPasswordTokenExpiresAt == null || user.ResetPasswordTokenExpiresAt < DateTime.UtcNow)
            return false;

        // Hash and save new password, clear token
        user.Password = HashPassword(request.NewPassword);
        user.ResetPasswordToken = null;
        user.ResetPasswordTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return true;
    }
}
