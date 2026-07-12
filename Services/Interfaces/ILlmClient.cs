namespace Fruitables.Services.Interfaces;

/// <summary>
/// Abstraction over a chat/completions LLM provider.
/// </summary>
public interface ILlmClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
