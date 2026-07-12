namespace Fruitables.Services.Chat;

public class ChatRateLimitException : Exception
{
    public ChatRateLimitException(string message) : base(message)
    {
    }
}
