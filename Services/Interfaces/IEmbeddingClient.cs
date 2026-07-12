namespace Fruitables.Services.Interfaces;

/// <summary>
/// Abstraction over a text-embedding provider.
/// </summary>
public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
