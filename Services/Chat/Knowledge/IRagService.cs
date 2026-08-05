using Fruitables.ViewModels;

namespace Fruitables.Services.Chat.Knowledge;

// ============================================================
// RAG = Retrieval-Augmented Generation
// (Tìm tri thức liên quan trước → rồi mới nhờ AI trả lời).
//
// Giống nhân viên: mở sổ FAQ → đọc đoạn đúng → trả lời khách.
// ============================================================
public interface IRagService
{
    Task<RagAnswer> AnswerAsync(string userMessage, CancellationToken ct = default);

    // Streaming: refuse (1 event) hoặc token… + complete; không bịa khi thiếu KB
    IAsyncEnumerable<RagStreamPart> AnswerStreamingAsync(
        string userMessage,
        CancellationToken ct = default);
}
