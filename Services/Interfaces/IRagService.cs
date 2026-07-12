using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

/// <summary>
/// Retrieves knowledge chunks and generates (or refuses) a RAG answer.
/// </summary>
public interface IRagService
{
    Task<RagAnswer> AnswerAsync(string userMessage, CancellationToken ct = default);
}
