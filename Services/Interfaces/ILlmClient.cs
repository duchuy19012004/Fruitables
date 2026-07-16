namespace Fruitables.Services.Interfaces;

// ============================================================
// "Cổng" gọi AI chat (Kimi, xAI, ...).
// Phần còn lại của app không cần biết đang dùng nhà cung cấp nào.
// ============================================================
public interface ILlmClient
{
    // Gửi lời dặn hệ thống + câu hỏi (có kèm context), nhận câu trả lời chữ
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);

    // Cùng prompt nhưng trả về từng mảnh chữ khi model đang generate (SSE từ provider)
    IAsyncEnumerable<string> CompleteStreamingAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);
}
