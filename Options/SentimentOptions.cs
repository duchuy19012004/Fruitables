namespace Fruitables.Options;

// Cấu hình module phân tích cảm xúc review — đọc từ appsettings.json mục "Sentiment".
// Provider LLM dùng chung cấu hình "Chat" (DeepSeek, OpenAI-compatible).
public class SentimentOptions
{
    public const string SectionName = "Sentiment";

    // Bật/tắt phân tích cảm xúc (tắt = không enqueue, không chạy backfill)
    public bool Enabled { get; set; } = true;

    // Số review tối đa gom vào 1 lần gọi LLM (chi phí: 1 call / batch)
    public int BatchSize { get; set; } = 15;

    // Số review / 1 outbox message khi backfill
    public int BackfillChunkSize { get; set; } = 15;

    // Severity >= ngưỡng này + nhãn tiêu cực → cảnh báo admin (SignalR)
    public int SevereThreshold { get; set; } = 3;

    // Số lần retry khi LLM trả JSON rỗng / không parse được
    public int RetryOnEmpty { get; set; } = 2;

    // Phiên bản prompt + rule dùng cho kết quả sentiment.
    public string AnalysisVersion { get; set; } = "sentiment-v2";

    // Conflict không được coi là chắc chắn dù LLM tự trả confidence cao.
    public float ConflictConfidenceCap { get; set; } = 0.6f;

    // Khi rating và comment trái chiều, bắt buộc admin xác nhận.
    public bool ManualReviewOnConflict { get; set; } = true;

    // Severity tối thiểu cho tín hiệu an toàn thực phẩm.
    public int SafetySeverity { get; set; } = 3;

    // Confidence dưới ngưỡng (hoặc null) → chưa đủ tin → bắt buộc duyệt tay,
    // không đưa vào KPI vận hành. Rating fallback (confidence = 1) không bị ảnh hưởng.
    public float MinConfidence { get; set; } = 0.5f;
}
