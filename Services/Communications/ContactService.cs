using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Infrastructure.Content;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Communications;

public class ContactService : IContactService
{
    private readonly ApplicationDbContext _db;
    private readonly IJsonDocumentSerializer _serializer;

    public ContactService(ApplicationDbContext db, IJsonDocumentSerializer? serializer = null)
    {
        _db = db;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    // Compatibility for tests that still construct via UnitOfWork.
    public ContactService(IUnitOfWork unitOfWork)
        : this(((Repositories.UnitOfWork)unitOfWork).Context)
    {
    }

    public async Task<ContactMessage> SendMessageAsync(string name, string email, string message)
    {
        var entry = ContentEntryMapper.FromContact(new ContactMessage
        {
            Name = name,
            Email = email,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        }, _serializer);
        _db.ContentEntries.Add(entry);
        await _db.SaveChangesAsync();
        entry.Key = ContentEntryMapper.Key("contact", entry.Id);
        await _db.SaveChangesAsync();
        return ContentEntryMapper.ToContact(entry, _serializer);
    }

    public async Task<List<ContactMessage>> GetAllMessagesAsync()
    {
        var entries = await _db.ContentEntries.AsNoTracking()
            .Where(entry => entry.EntryType == ContentEntryMapper.ContactType)
            .OrderByDescending(entry => entry.CreatedAt)
            .ToListAsync();
        return entries.Select(entry => ContentEntryMapper.ToContact(entry, _serializer)).ToList();
    }

    public async Task MarkAsReadAsync(int id)
    {
        var entry = await _db.ContentEntries.FirstOrDefaultAsync(item =>
            item.Id == id && item.EntryType == ContentEntryMapper.ContactType);
        if (entry == null)
            return;
        entry.IsRead = true;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.RowVersion = Guid.NewGuid().ToByteArray();
        await _db.SaveChangesAsync();
    }
}
