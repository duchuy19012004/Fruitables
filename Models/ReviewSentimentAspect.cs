namespace Fruitables.Models;

// Khía cạnh được khách nhắc đến trong comment.
public enum SentimentAspect
{
    Quality = 0,    // Chất lượng sản phẩm / độ tươi / vị
    Delivery = 1,   // Giao hàng / vận chuyển / thời gian
    Price = 2,      // Giá cả
    Packaging = 3,  // Đóng gói / bao bì
    Service = 4,    // Dịch vụ / chăm sóc khách hàng
    Other = 5       // Khác
}

/// <summary>
/// Cảm xúc của 1 khía cạnh trong review (1-n với ReviewSentiment).
/// </summary>
public class ReviewSentimentAspect
{
    public int Id { get; set; }

    public int ReviewSentimentId { get; set; }

    public SentimentAspect Aspect { get; set; }

    public SentimentLabel Sentiment { get; set; }

    public int? Severity { get; set; }

    public virtual ReviewSentiment ReviewSentiment { get; set; } = null!;
}
