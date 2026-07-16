namespace Fruitables.Models;

// ============================================================
// Nguồn tri thức mà bot học được lấy từ đâu.
// Giống như "kệ sách" khác nhau trong thư viện.
// ============================================================
public enum KnowledgeSourceType
{
    // Bài FAQ do CS viết tay
    Faq = 0,

    // Thông tin sản phẩm trên web (tên, mô tả...)
    Product = 1,

    // Cài đặt công khai (hotline, giờ làm việc...) — không lấy mật khẩu
    Setting = 2,

    // Tóm tắt catalog do server tạo (top bán chạy, nổi bật) — template sạch, không copy mô tả user
    Catalog = 3
}
