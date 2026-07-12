using System.Text;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat;

/// <summary>
/// Embeds the user question, ranks active knowledge chunks by cosine similarity,
/// and either generates an LLM answer from top context or refuses when scores are low.
/// </summary>
public sealed class RagService : IRagService
{
    internal const string RefuseMessage =
        "Xin lỗi, mình chưa có đủ thông tin trong hệ thống để trả lời chính xác câu này.\n" +
        "Bạn có thể liên hệ bộ phận CSKH qua trang Liên hệ hoặc để lại tin nhắn để được hỗ trợ trực tiếp.";

    private readonly ApplicationDbContext _db;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly ILlmClient _llmClient;
    private readonly ChatOptions _options;
    private readonly ILogger<RagService> _logger;

    public RagService(
        ApplicationDbContext db,
        IEmbeddingClient embeddingClient,
        ILlmClient llmClient,
        IOptions<ChatOptions> options,
        ILogger<RagService> logger)
    {
        _db = db;
        _embeddingClient = embeddingClient;
        _llmClient = llmClient;
        _options = options?.Value ?? new ChatOptions();
        _logger = logger;
    }

    public async Task<RagAnswer> AnswerAsync(string userMessage, CancellationToken ct = default)
    {
        var queryEmbedding = await _embeddingClient.EmbedAsync(userMessage ?? string.Empty, ct);

        var chunks = await _db.KnowledgeChunks
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        var scored = new List<(KnowledgeChunk Chunk, float Score)>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var vector = EmbeddingSerializer.FromJson(chunk.EmbeddingJson);
            var score = EmbeddingMath.CosineSimilarity(queryEmbedding, vector);
            scored.Add((chunk, score));
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        var topK = Math.Max(0, _options.TopK);
        var top = scored.Take(topK).ToList();
        var bestScore = top.Count > 0 ? top[0].Score : 0f;

        if (top.Count == 0 || bestScore < _options.MinScore)
        {
            _logger.LogInformation(
                "RAG refuse: chunks={ChunkCount}, bestScore={BestScore:F4}, minScore={MinScore:F4}",
                top.Count,
                bestScore,
                _options.MinScore);

            return new RagAnswer
            {
                Content = RefuseMessage,
                Refused = true,
                SourceChunkIds = new List<long>()
            };
        }

        var userPrompt = BuildUserPrompt(top.Select(t => t.Chunk).ToList(), userMessage ?? string.Empty);
        var content = await _llmClient.CompleteAsync(_options.SystemPrompt, userPrompt, ct);

        return new RagAnswer
        {
            Content = content,
            Refused = false,
            SourceChunkIds = top.Select(t => t.Chunk.Id).ToList()
        };
    }

    private static string BuildUserPrompt(IReadOnlyList<KnowledgeChunk> chunks, string question)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### CONTEXT");
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var title = string.IsNullOrWhiteSpace(chunk.Title) ? "(không tiêu đề)" : chunk.Title;
            sb.Append(i + 1);
            sb.Append(". ");
            sb.AppendLine(title);
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }

        sb.AppendLine("### QUESTION");
        sb.Append(question);
        return sb.ToString();
    }
}
