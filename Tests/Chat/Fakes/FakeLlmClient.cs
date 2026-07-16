using System.Runtime.CompilerServices;
using Fruitables.Services.Interfaces;

namespace Fruitables.Tests.Chat.Fakes;

/// <summary>
/// Test double for <see cref="ILlmClient"/> that records prompts and returns a fixed response.
/// Streaming yields the response in small chunks so stream tests exercise the pipeline.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    public string Response { get; set; } = "Câu trả lời giả.";

    /// <summary>Optional: force stream chunk size (chars). Default 8.</summary>
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
            // Yield control so async stream consumers work as in production
            await Task.Yield();
        }
    }
}
