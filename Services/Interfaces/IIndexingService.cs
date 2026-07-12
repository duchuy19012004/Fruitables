namespace Fruitables.Services.Interfaces;

/// <summary>
/// Builds and maintains knowledge-chunk embeddings for RAG retrieval.
/// </summary>
public interface IIndexingService
{
    Task IndexFaqAsync(int faqId, CancellationToken ct = default);

    Task IndexProductAsync(int productId, CancellationToken ct = default);

    Task IndexAllowlistedSettingsAsync(CancellationToken ct = default);

    Task ReindexAllAsync(CancellationToken ct = default);
}
