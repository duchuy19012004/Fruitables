using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.ViewModels;

/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống")]
    [MaxLength(200, ErrorMessage = "Họ tên không được vượt quá 200 ký tự")]
    [Display(Name = "Họ tên")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [MaxLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống")]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Result model for registration operation
/// </summary>
public class RegistrationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public User? User { get; set; }
    public RegistrationErrorType? ErrorType { get; set; }

    public static RegistrationResult Succeeded(User user) => new()
    {
        Success = true,
        User = user
    };

    public static RegistrationResult Failed(RegistrationErrorType errorType, string errorMessage) => new()
    {
        Success = false,
        ErrorType = errorType,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Error types for registration failures
/// </summary>
public enum RegistrationErrorType
{
    EmailAlreadyExists,
    PasswordMismatch,
    PasswordTooShort,
    PasswordMissingSpecialChar,
    InvalidEmail,
    ValidationError
}

/// <summary>
/// Request model for user login
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }
}

/// <summary>
/// Request model for forgot password (step 1: nhập email)
/// </summary>
public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Request model for reset password (step 2: đặt lại mật khẩu mới)
/// </summary>
public class ResetPasswordRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống")]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel cho trang Auth tab duy nhất (Login/Register)
/// </summary>
public class AuthPageViewModel
{
    public LoginRequest Login { get; set; } = new();
    public RegisterRequest Register { get; set; } = new();
    public string ActiveTab { get; set; } = "login";
}
