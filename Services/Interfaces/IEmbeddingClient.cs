namespace Fruitables.Services.Interfaces;

// ============================================================
// "Cổng" mã hóa chữ → dãy số (embedding) để so độ giống.
// Có thể là Local (trên máy) hoặc API ngoài.
// ============================================================
public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
