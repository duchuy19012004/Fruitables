using Fruitables.Models;

namespace Fruitables.Services.Communications;

public interface IContactService
{
    Task<ContactMessage> SendMessageAsync(string name, string email, string message);
    Task<List<ContactMessage>> GetAllMessagesAsync();
    Task MarkAsReadAsync(int id);
}
