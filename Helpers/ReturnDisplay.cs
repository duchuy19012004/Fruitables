using Fruitables.Models;
using Fruitables.Models.Returns;

namespace Fruitables.Helpers;

public static class ReturnDisplay
{
    public readonly record struct CustomerTimelineEntry(string Title, string? Note);

    public static string Text(ReturnRequestStatus value) => value switch
    {
        ReturnRequestStatus.Submitted => "Đã gửi",
        ReturnRequestStatus.AwaitingEvidence => "Chờ bổ sung bằng chứng",
        ReturnRequestStatus.UnderReview => "Đang thẩm định",
        ReturnRequestStatus.Approved => "Đã duyệt toàn bộ",
        ReturnRequestStatus.PartiallyApproved => "Đã duyệt một phần",
        ReturnRequestStatus.Rejected => "Đã từ chối",
        ReturnRequestStatus.ResolutionPending => "Đang xử lý phương án",
        ReturnRequestStatus.ResolutionFailed => "Xử lý thất bại",
        ReturnRequestStatus.Resolved => "Đã hoàn tất",
        ReturnRequestStatus.Cancelled => "Đã hủy",
        ReturnRequestStatus.Expired => "Đã quá hạn",
        _ => value.ToString()
    };

    public static string CustomerProgress(ReturnRequestStatus status) => status switch
    {
        ReturnRequestStatus.Submitted => "Đã tiếp nhận",
        ReturnRequestStatus.AwaitingEvidence => "Cần bổ sung",
        ReturnRequestStatus.UnderReview => "Đang xem xét",
        ReturnRequestStatus.Approved or
        ReturnRequestStatus.PartiallyApproved or
        ReturnRequestStatus.ResolutionPending or
        ReturnRequestStatus.ResolutionFailed => "Đang hoàn tiền",
        ReturnRequestStatus.Resolved => "Đã hoàn tiền",
        ReturnRequestStatus.Rejected => "Đã từ chối",
        ReturnRequestStatus.Cancelled => "Đã hủy",
        ReturnRequestStatus.Expired => "Đã quá hạn",
        _ => Text(status)
    };

    public static string Text(ReturnReasonCode value) => value switch
    {
        ReturnReasonCode.DamagedOrBruised => "Dập, vỡ hoặc hư hỏng",
        ReturnReasonCode.SpoiledOrMoldy => "Úng, thối hoặc mốc",
        ReturnReasonCode.TemperatureIssue => "Không bảo đảm nhiệt độ",
        ReturnReasonCode.WiltedOrNotFresh => "Héo hoặc không còn tươi",
        ReturnReasonCode.WrongItem => "Giao sai sản phẩm",
        ReturnReasonCode.MissingItem => "Giao thiếu sản phẩm",
        ReturnReasonCode.UnderweightOrShortQuantity => "Thiếu cân hoặc thiếu số lượng",
        ReturnReasonCode.LateDeliveryCausedDamage => "Giao trễ làm giảm chất lượng",
        ReturnReasonCode.FoodSafetyConcern => "Nghi ngờ an toàn thực phẩm",
        ReturnReasonCode.ChangeOfMind => "Thay đổi nhu cầu",
        ReturnReasonCode.Other => "Lý do khác",
        _ => value.ToString()
    };

    public static string Text(ReturnResolutionType value) => value switch
    {
        ReturnResolutionType.None => "Chưa chọn",
        ReturnResolutionType.PartialRefund => "Hoàn tiền phần bị ảnh hưởng",
        ReturnResolutionType.FullRefund => "Hoàn toàn bộ sản phẩm",
        ReturnResolutionType.Replacement => "Giao bù",
        ReturnResolutionType.StoreCredit => "Cộng số dư cửa hàng",
        ReturnResolutionType.Reject => "Từ chối",
        _ => value.ToString()
    };

    public static string Text(RefundStatus value) => value switch
    {
        RefundStatus.Pending => "Chờ xử lý",
        RefundStatus.AwaitingDestination => "Chờ thông tin nhận tiền",
        RefundStatus.AwaitingApproval => "Chờ phê duyệt",
        RefundStatus.Processing => "Đang chuyển tiền",
        RefundStatus.Succeeded => "Đã hoàn tiền",
        RefundStatus.Failed => "Hoàn tiền thất bại",
        RefundStatus.Cancelled => "Đã hủy",
        _ => value.ToString()
    };

    public static bool ShowFullRefundDestination(RefundStatus status) =>
        status is RefundStatus.AwaitingApproval or RefundStatus.Processing;

    public static string Text(RefundMethod value) => value switch
    {
        RefundMethod.ManualBankTransfer => "Chuyển khoản thủ công",
        RefundMethod.OriginalPaymentMethod => "Phương thức thanh toán ban đầu",
        RefundMethod.StoreCredit => "Số dư cửa hàng",
        _ => value.ToString()
    };

    public static string Text(InventoryDispositionType value) => value switch
    {
        InventoryDispositionType.NotReturned => "Không thu hồi hàng",
        InventoryDispositionType.Quarantined => "Cách ly kiểm tra",
        InventoryDispositionType.Discarded => "Tiêu hủy",
        InventoryDispositionType.Donated => "Chuyển tặng",
        InventoryDispositionType.ReturnedToSupplier => "Trả nhà cung cấp",
        InventoryDispositionType.Restocked => "Nhập lại kho bán",
        _ => value.ToString()
    };

    public static string Text(EvidenceScanStatus value) => value switch
    {
        EvidenceScanStatus.Pending => "Chờ kiểm tra",
        EvidenceScanStatus.Clean => "An toàn",
        EvidenceScanStatus.Rejected => "Không hợp lệ",
        EvidenceScanStatus.ScanFailed => "Kiểm tra thất bại",
        _ => value.ToString()
    };

    public static CustomerTimelineEntry? CustomerTimelineEvent(ReturnEvent item) => item.Type switch
    {
        ReturnEventType.Submitted => new("Đã gửi yêu cầu", "Yêu cầu của bạn đã được tiếp nhận."),
        ReturnEventType.EvidenceAdded => new("Đã bổ sung bằng chứng", "Đã nhận thêm bằng chứng từ bạn."),
        ReturnEventType.EvidenceRequested => new("Cần bổ sung bằng chứng", item.Note),
        ReturnEventType.Approved => new("Yêu cầu đã được duyệt", item.Note),
        ReturnEventType.PartiallyApproved => new("Yêu cầu đã được duyệt một phần", item.Note),
        ReturnEventType.Rejected => new("Yêu cầu đã bị từ chối", item.Note),
        ReturnEventType.Cancelled => new("Yêu cầu đã bị hủy", item.Note),
        ReturnEventType.Expired => new("Yêu cầu đã quá hạn", item.Note),
        ReturnEventType.RefundCreated => new("Đã tạo khoản hoàn tiền", item.Note),
        ReturnEventType.RefundDestinationSubmitted => new("Đã nhận thông tin nhận tiền", "Thông tin nhận tiền của bạn đã được ghi nhận."),
        ReturnEventType.RefundDestinationCorrectionRequested => new("Cần cập nhật thông tin nhận tiền", "Thông tin nhận tiền cần được cập nhật. Vui lòng kiểm tra và gửi lại."),
        ReturnEventType.RefundSucceeded => new("Đã hoàn tiền", "Khoản hoàn tiền đã được xác nhận thành công."),
        _ => null
    };

    public static string Text(ReturnEventType value) => value switch
    {
        ReturnEventType.Submitted => "Khách hàng đã gửi yêu cầu",
        ReturnEventType.EvidenceAdded => "Đã bổ sung bằng chứng",
        ReturnEventType.EvidenceRequested => "Yêu cầu bổ sung bằng chứng",
        ReturnEventType.ReviewStarted => "Bắt đầu thẩm định",
        ReturnEventType.Approved => "Duyệt toàn bộ",
        ReturnEventType.PartiallyApproved => "Duyệt một phần",
        ReturnEventType.Rejected => "Từ chối yêu cầu",
        ReturnEventType.Cancelled => "Hủy yêu cầu",
        ReturnEventType.Expired => "Yêu cầu quá hạn",
        ReturnEventType.ResolutionStarted => "Bắt đầu thực hiện phương án",
        ReturnEventType.ResolutionFailed => "Thực hiện phương án thất bại",
        ReturnEventType.Resolved => "Hoàn tất yêu cầu",
        ReturnEventType.RefundCreated => "Tạo khoản hoàn tiền",
        ReturnEventType.RefundSucceeded => "Hoàn tiền thành công",
        ReturnEventType.RefundFailed => "Hoàn tiền thất bại",
        ReturnEventType.DispositionRecorded => "Ghi nhận xử lý hàng",
        ReturnEventType.RefundDestinationSubmitted => "Khách hàng đã cung cấp thông tin nhận tiền",
        ReturnEventType.RefundDestinationViewed => "Bộ phận tài chính đã xem thông tin nhận tiền",
        ReturnEventType.RefundProcessingStarted => "Bộ phận tài chính bắt đầu xử lý",
        ReturnEventType.RefundDestinationCorrectionRequested => "Yêu cầu cập nhật thông tin nhận tiền",
        _ => value.ToString()
    };

    public static string Text(PaymentMethod value) => value switch
    {
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.Check => "Séc",
        PaymentMethod.COD => "Thanh toán khi nhận hàng",
        PaymentMethod.Paypal => "PayPal",
        _ => value.ToString()
    };

    public static string Text(PaymentStatus value) => value switch
    {
        PaymentStatus.Pending => "Chờ thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.PartiallyRefunded => "Đã hoàn một phần",
        PaymentStatus.Refunded => "Đã hoàn toàn bộ",
        _ => value.ToString()
    };
}
