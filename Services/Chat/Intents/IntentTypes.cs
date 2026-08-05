namespace Fruitables.Services.Chat.Intents;

// Phân loại ý định khách hàng — mỗi tin nhắn → 1 intent.
public enum ChatIntentKind
{
    GeneralInquiry,   // câu hỏi chung FAQ / chính sách
    ProductLookup,    // tìm sản phẩm
    OrderStatus,      // tra cứu đơn hàng
    CouponCheck,      // kiểm tra mã giảm giá
    ShippingQuote,    // hỏi phí ship
    SmallTalk,        // chào hỏi / xã giao
    OutOfScope        // ngoài phạm vi / prompt injection
}

// Kết quả phân loại: kind + confidence + slots trích xuất.
public sealed record ChatIntent
{
    public required ChatIntentKind Kind { get; init; }
    public required float Confidence { get; init; }

    // Tham số trích xuất (mã đơn, tên SP, ...)
    public IReadOnlyDictionary<string, string> Slots { get; init; }
        = new Dictionary<string, string>();

    public static ChatIntent Of(ChatIntentKind kind, float confidence = 1.0f)
        => new() { Kind = kind, Confidence = confidence };
}
