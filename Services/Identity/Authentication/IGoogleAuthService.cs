using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Identity.Authentication;

public interface IGoogleAuthService
{
    // Kiểm tra Google Auth có được bật trong DB không
    Task<bool> IsGoogleAuthEnabledAsync();

    Task<GoogleAuthResult> ProcessGoogleLoginAsync(string email, string? name, string googleId);

    Task<(User? User, string? LockReason)> GetOrCreateUserFromGoogleAsync(string email, string? name, string googleId);
}
