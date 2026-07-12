using System.Security.Cryptography;
using System.Text;
using Fruitables.Constants;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat;

/// <summary>
/// Indexes FAQs, products, and allowlisted settings into <see cref="KnowledgeChunk"/> rows
/// with content-hash deduplication to avoid unnecessary re-embedding.
/// </summary>
public sealed class IndexingService : IIndexingService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly ILogger<IndexingService> _logger;

    // Reserved for future chunk-size / embedding-model knobs from configuration.
    private readonly ChatOptions _options;

    public IndexingService(
        ApplicationDbContext db,
        IEmbeddingClient embeddingClient,
        IOptions<ChatOptions> options,
        ILogger<IndexingService> logger)
    {
        _db = db;
        _embeddingClient = embeddingClient;
        _options = options?.Value ?? new ChatOptions();
        _logger = logger;
    }

    public async Task IndexFaqAsync(int faqId, CancellationToken ct = default)
    {
        var sourceId = faqId.ToString();
        var faq = await _db.Faqs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faqId, ct);

        if (faq is null || !faq.IsActive)
        {
            await DeactivateSourceAsync(KnowledgeSourceType.Faq, sourceId, ct);
            return;
        }

        var text = $"{faq.Title}\n\n{faq.Body}";
        var chunks = TextChunker.Chunk(text);
        await UpsertChunksAsync(KnowledgeSourceType.Faq, sourceId, faq.Title, chunks, ct);

        _logger.LogDebug("Indexed FAQ {FaqId} into {ChunkCount} chunk(s)", faqId, chunks.Count);
    }

    public async Task IndexProductAsync(int productId, CancellationToken ct = default)
    {
        var sourceId = productId.ToString();
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null || product.IsDeleted || !product.IsActive)
        {
            await DeactivateSourceAsync(KnowledgeSourceType.Product, sourceId, ct);
            return;
        }

        var text = BuildProductText(product);
        var chunks = TextChunker.Chunk(text);
        await UpsertChunksAsync(KnowledgeSourceType.Product, sourceId, product.Name, chunks, ct);

        _logger.LogDebug("Indexed product {ProductId} into {ChunkCount} chunk(s)", productId, chunks.Count);
    }

    public async Task IndexAllowlistedSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _db.Settings.AsNoTracking().ToListAsync(ct);
        var byKey = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var indexedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in ChatSettingAllowlist.Keys)
        {
            byKey.TryGetValue(key, out var value);
            if (string.IsNullOrWhiteSpace(value))
            {
                await DeactivateSourceAsync(KnowledgeSourceType.Setting, key, ct);
                continue;
            }

            var content = $"{key}: {value}";
            await UpsertChunksAsync(
                KnowledgeSourceType.Setting,
                key,
                key,
                new[] { content },
                ct);
            indexedKeys.Add(key);
        }

        // Deactivate any active setting chunks that are not currently indexed (non-allowlisted or emptied).
        var activeSettingChunks = await _db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Setting && c.IsActive)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var deactivated = 0;
        foreach (var chunk in activeSettingChunks)
        {
            if (!indexedKeys.Contains(chunk.SourceId))
            {
                chunk.IsActive = false;
                chunk.UpdatedAt = now;
                deactivated++;
            }
        }

        if (deactivated > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogDebug(
            "Indexed {IndexedCount} allowlisted setting(s); deactivated {DeactivatedCount} stale setting chunk(s)",
            indexedKeys.Count,
            deactivated);
    }

    public async Task ReindexAllAsync(CancellationToken ct = default)
    {
        var faqIds = await _db.Faqs.AsNoTracking().Select(f => f.Id).ToListAsync(ct);
        foreach (var id in faqIds)
        {
            await IndexFaqAsync(id, ct);
        }

        var productIds = await _db.Products.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        foreach (var id in productIds)
        {
            await IndexProductAsync(id, ct);
        }

        await IndexAllowlistedSettingsAsync(ct);

        _logger.LogInformation(
            "ReindexAll complete: {FaqCount} FAQ(s), {ProductCount} product(s), settings",
            faqIds.Count,
            productIds.Count);
    }

    private async Task DeactivateSourceAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        CancellationToken ct)
    {
        var chunks = await _db.KnowledgeChunks
            .Where(c => c.SourceType == sourceType && c.SourceId == sourceId && c.IsActive)
            .ToListAsync(ct);

        if (chunks.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var chunk in chunks)
        {
            chunk.IsActive = false;
            chunk.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertChunksAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        string? title,
        IReadOnlyList<string> chunkTexts,
        CancellationToken ct)
    {
        var existing = await _db.KnowledgeChunks
            .Where(c => c.SourceType == sourceType && c.SourceId == sourceId)
            .ToListAsync(ct);

        var newHashes = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        // Track which existing rows we reused so we don't double-match the same row.
        var reusedIds = new HashSet<long>();

        foreach (var text in chunkTexts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var hash = ComputeContentHash(text);
            if (!newHashes.Add(hash))
            {
                // Duplicate chunk text within this source — keep a single active row.
                continue;
            }

            var match = existing.FirstOrDefault(c =>
                    c.ContentHash == hash && c.IsActive && !reusedIds.Contains(c.Id))
                ?? existing.FirstOrDefault(c =>
                    c.ContentHash == hash && !reusedIds.Contains(c.Id));

            if (match is not null)
            {
                reusedIds.Add(match.Id);
                match.IsActive = true;
                match.Title = title;
                match.Content = text;
                match.UpdatedAt = now;
                // ContentHash match: skip re-embed.
                continue;
            }

            var embedding = await _embeddingClient.EmbedAsync(text, ct);
            _db.KnowledgeChunks.Add(new KnowledgeChunk
            {
                SourceType = sourceType,
                SourceId = sourceId,
                Title = title,
                Content = text,
                EmbeddingJson = EmbeddingSerializer.ToJson(embedding),
                ContentHash = hash,
                IsActive = true,
                UpdatedAt = now
            });
        }

        foreach (var chunk in existing)
        {
            if (!newHashes.Contains(chunk.ContentHash) && chunk.IsActive)
            {
                chunk.IsActive = false;
                chunk.UpdatedAt = now;
            }
            else if (newHashes.Contains(chunk.ContentHash)
                     && chunk.IsActive
                     && !reusedIds.Contains(chunk.Id))
            {
                // Extra duplicate rows with the same hash that were not reused.
                chunk.IsActive = false;
                chunk.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    internal static string BuildProductText(Product product)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(product.Name))
            parts.Add(product.Name.Trim());
        if (!string.IsNullOrWhiteSpace(product.ShortDescription))
            parts.Add(product.ShortDescription.Trim());
        if (!string.IsNullOrWhiteSpace(product.Description))
            parts.Add(product.Description.Trim());
        if (product.Category is not null && !string.IsNullOrWhiteSpace(product.Category.Name))
            parts.Add(product.Category.Name.Trim());
        if (!string.IsNullOrWhiteSpace(product.Unit))
            parts.Add(product.Unit.Trim());
        if (!string.IsNullOrWhiteSpace(product.CountryOrigin))
            parts.Add(product.CountryOrigin.Trim());

        return string.Join("\n\n", parts);
    }

    internal static string ComputeContentHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
