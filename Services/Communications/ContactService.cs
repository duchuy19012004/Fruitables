using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;

namespace Fruitables.Services.Communications;

public class ContactService : IContactService
{
    private readonly IUnitOfWork _unitOfWork;

    public ContactService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ContactMessage> SendMessageAsync(string name, string email, string message)
    {
        var contactMessage = new ContactMessage
        {
            Name = name,
            Email = email,
            Message = message
        };

        await _unitOfWork.Contacts.AddAsync(contactMessage);
        await _unitOfWork.SaveChangesAsync();

        return contactMessage;
    }

    public async Task<List<ContactMessage>> GetAllMessagesAsync()
    {
        return await _unitOfWork.Contacts.Query()
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int id)
    {
        var message = await _unitOfWork.Contacts.GetByIdAsync(id);
        if (message != null)
        {
            message.IsRead = true;
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
