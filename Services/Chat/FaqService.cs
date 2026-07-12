using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fruitables.Services.Chat;

public sealed class FaqService : IFaqService
{
    private readonly ApplicationDbContext _db;
    private readonly IIndexingService _indexingService;
    private readonly ILogger<FaqService> _logger;

    public FaqService(
        ApplicationDbContext db,
        IIndexingService indexingService,
        ILogger<FaqService> logger)
    {
        _db = db;
        _indexingService = indexingService;
        _logger = logger;
    }

    public async Task<List<Faq>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Faqs
            .AsNoTracking()
            .OrderByDescending(f => f.Id)
            .ToListAsync(ct);
    }

    public async Task<Faq?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Faqs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<Faq> CreateAsync(
        string title,
        string body,
        string category,
        bool isActive,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var faq = new Faq
        {
            Title = title.Trim(),
            Body = body.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim(),
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Faqs.Add(faq);
        await _db.SaveChangesAsync(ct);

        await TryIndexAsync(faq.Id, ct);
        return faq;
    }

    public async Task<Faq?> UpdateAsync(
        int id,
        string title,
        string body,
        string category,
        bool isActive,
        CancellationToken ct = default)
    {
        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (faq is null)
            return null;

        faq.Title = title.Trim();
        faq.Body = body.Trim();
        faq.Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim();
        faq.IsActive = isActive;
        faq.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await TryIndexAsync(faq.Id, ct);
        return faq;
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (faq is null)
            return;

        faq.IsActive = isActive;
        faq.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await TryIndexAsync(faq.Id, ct);
    }

    public async Task ReindexAllAsync(CancellationToken ct = default)
    {
        await _indexingService.ReindexAllAsync(ct);
    }

    private async Task TryIndexAsync(int faqId, CancellationToken ct)
    {
        try
        {
            await _indexingService.IndexFaqAsync(faqId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index FAQ {FaqId}; CRUD operation still succeeded", faqId);
        }
    }
}
