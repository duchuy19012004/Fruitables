namespace Fruitables.Models;

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public User? User { get; set; }

    public static AuthResult Succeeded(User user) => new() { Success = true, User = user };
    public static AuthResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
