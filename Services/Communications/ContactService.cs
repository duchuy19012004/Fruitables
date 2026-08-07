using Fruitables.Data;
using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Services.Communications;

namespace Fruitables.Services.Communications;

public class ContactService : IContactService
{
    private readonly ApplicationDbContext _db;

    public ContactService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ContactMessage> SendMessageAsync(string name, string email, string message)
    {
        var contactMessage = new ContactMessage
        {
            Name = name,
            Email = email,
            Message = message
        };

        await _db.ContactMessages.AddAsync(contactMessage);
        await _db.SaveChangesAsync();

        return contactMessage;
    }

    public async Task<List<ContactMessage>> GetAllMessagesAsync()
    {
        return await _db.ContactMessages
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int id)
    {
        var message = await _db.ContactMessages.FindAsync(id);
        if (message != null)
        {
            message.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }
}
