using System.Text.Json;

namespace Fruitables.Services.Interfaces;

// Cổng gọi AI chat qua endpoint OpenAI-compatible.
public interface ILlmClient
{
    // Gửi prompt, nhận câu trả lời chữ
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);

    // Streaming: trả về từng chunk
    IAsyncEnumerable<string> CompleteStreamingAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);

    // Gọi LLM và parse JSON response (dùng cho intent classification, structured output)
    Task<JsonElement> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
