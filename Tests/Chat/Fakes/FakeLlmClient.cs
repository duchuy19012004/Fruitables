using System.Runtime.CompilerServices;
using System.Text.Json;
using Fruitables.Services.Interfaces;

namespace Fruitables.Tests.Chat.Fakes;

// Test double for ILlmClient: records prompts, returns fixed response.
public sealed class FakeLlmClient : ILlmClient
{
    public string Response { get; set; } = "Câu trả lời giả.";

    // JSON response cho GenerateAsync (intent classification)
    public string JsonResponse { get; set; } = """{"kind":"GeneralInquiry","confidence":0.9,"slots":{}}""";

    public int StreamChunkSize { get; set; } = 8;

    public List<(string System, string User)> Calls { get; } = new();

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add((systemPrompt, userPrompt));
        return Task.FromResult(Response);
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add((systemPrompt, userPrompt));

        var text = Response ?? string.Empty;
        var size = StreamChunkSize > 0 ? StreamChunkSize : 8;
        for (var i = 0; i < text.Length; i += size)
        {
            ct.ThrowIfCancellationRequested();
            var len = Math.Min(size, text.Length - i);
            yield return text.Substring(i, len);
            await Task.Yield();
        }
    }

    public Task<JsonElement> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add((systemPrompt, userPrompt));

        var doc = JsonDocument.Parse(JsonResponse);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
