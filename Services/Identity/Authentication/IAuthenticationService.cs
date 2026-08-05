using Fruitables.Models;

namespace Fruitables.Services.Identity.Authentication;

public interface IAuthenticationService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<User?> GetUserByEmailAsync(string email);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
