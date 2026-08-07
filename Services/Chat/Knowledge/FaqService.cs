using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Content;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fruitables.Services.Chat.Knowledge;

public sealed class FaqService : IFaqService
{
    private readonly ApplicationDbContext _db;
    private readonly IIndexingService _indexingService;
    private readonly ILogger<FaqService> _logger;
    private readonly IJsonDocumentSerializer _serializer;

    public FaqService(
        ApplicationDbContext db,
        IIndexingService indexingService,
        ILogger<FaqService> logger,
        IJsonDocumentSerializer? serializer = null)
    {
        _db = db;
        _indexingService = indexingService;
        _logger = logger;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    public async Task<List<Faq>> GetAllAsync(CancellationToken ct = default)
    {
        var entries = await _db.ContentEntries.AsNoTracking()
            .Where(entry => entry.EntryType == ContentEntryMapper.FaqType)
            .OrderByDescending(entry => entry.Id)
            .ToListAsync(ct);
        return entries.Select(entry => ContentEntryMapper.ToFaq(entry, _serializer)).ToList();
    }

    public async Task<Faq?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entry = await _db.ContentEntries.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.EntryType == ContentEntryMapper.FaqType, ct);
        return entry is null ? null : ContentEntryMapper.ToFaq(entry, _serializer);
    }

    public async Task<Faq> CreateAsync(
        string title,
        string body,
        string category,
        bool isActive,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var entry = ContentEntryMapper.FromFaq(new Faq
        {
            Title = title.Trim(),
            Body = body.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim(),
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now
        }, _serializer);
        _db.ContentEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        entry.Key = ContentEntryMapper.Key("faq", entry.Id);
        await _db.SaveChangesAsync(ct);
        await TryIndexAsync(entry.Id, ct);
        return ContentEntryMapper.ToFaq(entry, _serializer);
    }

    public async Task<Faq?> UpdateAsync(
        int id,
        string title,
        string body,
        string category,
        bool isActive,
        CancellationToken ct = default)
    {
        var entry = await _db.ContentEntries.FirstOrDefaultAsync(
            item => item.Id == id && item.EntryType == ContentEntryMapper.FaqType, ct);
        if (entry is null)
            return null;

        ContentEntryMapper.FromFaq(new Faq
        {
            Id = id,
            Title = title.Trim(),
            Body = body.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim(),
            IsActive = isActive,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        }, _serializer, entry);
        await _db.SaveChangesAsync(ct);
        await TryIndexAsync(entry.Id, ct);
        return ContentEntryMapper.ToFaq(entry, _serializer);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var entry = await _db.ContentEntries.FirstOrDefaultAsync(
            item => item.Id == id && item.EntryType == ContentEntryMapper.FaqType, ct);
        if (entry is null)
            return;

        var faq = ContentEntryMapper.ToFaq(entry, _serializer);
        faq.IsActive = isActive;
        ContentEntryMapper.FromFaq(faq, _serializer, entry);
        await _db.SaveChangesAsync(ct);
        await TryIndexAsync(entry.Id, ct);
    }

    public Task ReindexAllAsync(CancellationToken ct = default) =>
        _indexingService.ReindexAllAsync(ct);

    private async Task TryIndexAsync(int faqId, CancellationToken ct)
    {
        try
        {
            await _indexingService.IndexFaqAsync(faqId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index FAQ {FaqId}; content was still saved.", faqId);
        }
    }
}
