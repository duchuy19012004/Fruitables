using System.Runtime.CompilerServices;
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

// ============================================================
// BỘ NÃO TRẢ LỜI CỦA BOT (RAG)
//
// Quy trình dễ hiểu (4 bước):
// 1) Đổi câu hỏi khách thành "vân tay số"
// 2) So với từng mẩu tri thức trong DB → chọn mẩu giống nhất
// 3) Nếu giống quá thấp → nói thật "chưa có thông tin" (không bịa)
// 4) Nếu đủ giống → đưa các mẩu đó + câu hỏi cho AI viết câu trả lời
// ============================================================
public sealed class RagService : IRagService
{
    // Câu trả lời cố định khi hệ thống không tìm thấy tri thức đủ tin cậy
    internal const string RefuseMessage =
        "Mình chưa tìm thấy thông tin phù hợp để trả lời câu này.";

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

    // Sensitive patterns - defense-in-depth (ChatService cũng check)
    private static readonly string[] SensitivePatterns = new[]
    {
        "admin", "quản trị", "mật khẩu", "password", "api key", "connection string",
        "debug", "config", "secret", "token", "credential", "database"
    };

    // Trả lời 1 câu hỏi của khách (đủ câu, không stream)
    public async Task<RagAnswer> AnswerAsync(string userMessage, CancellationToken ct = default)
    {
        // Sensitive guard: chặn trước khi gọi embedding/LLM
        if (IsSensitive(userMessage))
        {
            return new RagAnswer
            {
                Content = RefuseMessage,
                Refused = true,
                SourceChunkIds = new List<long>()
            };
        }

        var retrieval = await RetrieveAsync(userMessage, ct);
        if (retrieval is null)
        {
            return new RagAnswer
            {
                Content = RefuseMessage,
                Refused = true,
                SourceChunkIds = new List<long>()
            };
        }

        var userPrompt = BuildUserPrompt(retrieval.TopChunks, userMessage ?? string.Empty);
        var content = await _llmClient.CompleteAsync(_options.SystemPrompt, userPrompt, ct);

        return new RagAnswer
        {
            Content = content,
            Refused = false,
            SourceChunkIds = retrieval.SourceChunkIds
        };
    }

    // Streaming: refuse 1 phát, hoặc token… rồi complete
    public async IAsyncEnumerable<RagStreamPart> AnswerStreamingAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Sensitive guard
        if (IsSensitive(userMessage))
        {
            yield return RagStreamPart.Refuse(RefuseMessage);
            yield break;
        }

        var retrieval = await RetrieveAsync(userMessage, ct);
        if (retrieval is null)
        {
            yield return RagStreamPart.Refuse(RefuseMessage);
            yield break;
        }

        var userPrompt = BuildUserPrompt(retrieval.TopChunks, userMessage ?? string.Empty);
        var sb = new StringBuilder();

        await foreach (var delta in _llmClient.CompleteStreamingAsync(
                           _options.SystemPrompt, userPrompt, ct))
        {
            if (string.IsNullOrEmpty(delta))
                continue;

            sb.Append(delta);
            yield return RagStreamPart.Token(delta);
        }

        yield return RagStreamPart.Complete(sb.ToString(), retrieval.SourceChunkIds);
    }

    private sealed class RetrievalResult
    {
        public required List<KnowledgeChunk> TopChunks { get; init; }
        public required List<long> SourceChunkIds { get; init; }
    }

    // null = refuse (không đủ tin cậy)
    private async Task<RetrievalResult?> RetrieveAsync(string userMessage, CancellationToken ct)
    {
        // Bước 1: mã hóa câu hỏi
        var queryEmbedding = await _embeddingClient.EmbedAsync(userMessage ?? string.Empty, ct);

        // Bước 2: lấy mọi mẩu tri thức đang bật
        var chunks = await _db.KnowledgeChunks
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        // Chấm điểm hybrid: cosine (embedding) + coverage từ khóa/synonym
        // LocalHash không “hiểu nghĩa” sâu → lexical giúp “Phí ship?” match FAQ vận chuyển
        var scored = new List<(KnowledgeChunk Chunk, float Score, float Emb, float Lex)>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var vector = EmbeddingSerializer.FromJson(chunk.EmbeddingJson);
            var emb = EmbeddingMath.CosineSimilarity(queryEmbedding, vector);
            var docText = string.IsNullOrWhiteSpace(chunk.Title)
                ? chunk.Content
                : chunk.Title + "\n" + chunk.Content;
            var lex = RetrievalText.QueryCoverage(userMessage ?? string.Empty, docText);
            // Ưu tiên điểm cao hơn giữa 2 kênh; blend nhẹ để ổn định ranking
            var score = Math.Max(emb, lex) * 0.85f + Math.Min(emb, lex) * 0.15f;
            scored.Add((chunk, score, emb, lex));
        }

        // Cao → thấp
        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Chỉ giữ top K mẩu tốt nhất
        var topK = Math.Max(0, _options.TopK);
        var top = scored.Take(topK).ToList();
        var bestScore = top.Count > 0 ? top[0].Score : 0f;
        var bestEmb = top.Count > 0 ? top[0].Emb : 0f;
        var bestLex = top.Count > 0 ? top[0].Lex : 0f;

        // Bước 3: không đủ tin cậy → từ chối (an toàn hơn bịa)
        if (top.Count == 0 || bestScore < _options.MinScore)
        {
            _logger.LogInformation(
                "RAG refuse: chunks={ChunkCount}, bestScore={BestScore:F4} (emb={Emb:F4}, lex={Lex:F4}), minScore={MinScore:F4}",
                top.Count,
                bestScore,
                bestEmb,
                bestLex,
                _options.MinScore);

            return null;
        }

        _logger.LogInformation(
            "RAG hit: bestScore={BestScore:F4} (emb={Emb:F4}, lex={Lex:F4}), sources={SourceCount}",
            bestScore,
            bestEmb,
            bestLex,
            top.Count);

        var topChunks = top.Select(t => t.Chunk).ToList();
        return new RetrievalResult
        {
            TopChunks = topChunks,
            SourceChunkIds = topChunks.Select(c => c.Id).ToList()
        };
    }

    // Ghép "sổ tay" context + câu hỏi thành 1 prompt gửi AI
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

    private static bool IsSensitive(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var lower = message.ToLowerInvariant();
        return SensitivePatterns.Any(p => lower.Contains(p));
    }
}
