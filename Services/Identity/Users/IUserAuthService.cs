using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Identity.Users;

/// <summary>
/// Interface for user authentication service (registration, login, logout)
/// </summary>
public interface IUserAuthService
{
    /// <summary>
    /// Register a new user account
    /// </summary>
    Task<RegistrationResult> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Login with email and password
    /// </summary>
    Task<AuthResult> LoginAsync(string email, string password);

    /// <summary>
    /// Logout current user
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Get user by email
    /// </summary>
    Task<User?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Get redirect URL based on user role
    /// Customer -> /Home/Index
    /// Admin/SuperAdmin -> /Admin/Dashboard
    /// </summary>
    string GetRedirectUrlByRole(UserRole role);

    /// <summary>
    /// Hash password using BCrypt
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verify password against BCrypt hash
    /// </summary>
    bool VerifyPassword(string password, string hash);

    /// <summary>
    /// Generate a password reset token, save to DB, and send reset email
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="resetCallbackUrl">The full URL of the reset-password page</param>
    /// <returns>True if token generated and email sent (or email not found - for security)</returns>
    Task<bool> GeneratePasswordResetTokenAsync(string email, string resetCallbackUrl);

    /// <summary>
    /// Validate token and reset user's password
    /// </summary>
    /// <param name="request">Reset password request containing email, token and new password</param>
    /// <returns>True if reset successful, false if token invalid/expired</returns>
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
}
