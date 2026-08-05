namespace Fruitables.Services.Chat.Knowledge;

// ============================================================
// Đưa dữ liệu (FAQ, sản phẩm, cài đặt) vào "sổ tri thức" KnowledgeChunks
// để bot có thể tìm và trả lời.
//
// Nút Admin "Đồng bộ knowledge" gọi ReindexAllAsync.
// ============================================================
public interface IIndexingService
{
    Task IndexFaqAsync(int faqId, CancellationToken ct = default);
    Task IndexProductAsync(int productId, CancellationToken ct = default);
    Task IndexAllowlistedSettingsAsync(CancellationToken ct = default);
    /// <summary>Top bán chạy (từ đơn) + sản phẩm nổi bật — chunk template server-side.</summary>
    Task IndexCatalogInsightsAsync(CancellationToken ct = default);

    /// <summary>Tóm tắt cảm xúc đánh giá 1 sản phẩm (số liệu + khía cạnh bị chê) cho chatbot.</summary>
    Task IndexProductReviewSummaryAsync(int productId, CancellationToken ct = default);

    Task ReindexAllAsync(CancellationToken ct = default);
}
