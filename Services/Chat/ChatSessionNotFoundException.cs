namespace Fruitables.Services.Chat;

public class ChatSessionNotFoundException : Exception
{
    public ChatSessionNotFoundException(string message) : base(message)
    {
    }
}
