namespace Fruitables.Models;

/// <summary>
/// Các loại vi phạm theo chính sách (Violation Policy)
/// </summary>
public static class ViolationTypes
{
    public const string Spam = "Spam hoặc lạm dụng hệ thống";
    public const string PaymentFraud = "Gian lận thanh toán hoặc hoàn tiền";
    public const string Harassment = "Hành vi quấy rối nhân viên hoặc khách hàng khác";
    public const string TermsViolation = "Vi phạm điều khoản sử dụng";
    public const string LegalRequest = "Yêu cầu từ cơ quan pháp luật";
    public const string Other = "Lý do khác";

    /// <summary>
    /// Danh sách tất cả các loại vi phạm hợp lệ
    /// </summary>
    public static readonly string[] All = new[]
    {
        Spam,
        PaymentFraud,
        Harassment,
        TermsViolation,
        LegalRequest,
        Other
    };

    /// <summary>
    /// Kiểm tra loại vi phạm có hợp lệ không
    /// </summary>
    public static bool IsValid(string? violationType)
    {
        return !string.IsNullOrEmpty(violationType) && All.Contains(violationType);
    }
}
