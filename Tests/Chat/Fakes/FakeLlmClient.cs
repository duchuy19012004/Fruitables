using Fruitables.Services.Interfaces;

namespace Fruitables.Tests.Chat.Fakes;

/// <summary>
/// Test double for <see cref="ILlmClient"/> that records prompts and returns a fixed response.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    public string Response { get; set; } = "Câu trả lời giả.";

    public List<(string System, string User)> Calls { get; } = new();

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add((systemPrompt, userPrompt));
        return Task.FromResult(Response);
    }
}
