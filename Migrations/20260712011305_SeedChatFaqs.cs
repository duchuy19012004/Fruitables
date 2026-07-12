using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class SeedChatFaqs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Faqs",
                columns: new[] { "Id", "Body", "Category", "CreatedAt", "IsActive", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Phí vận chuyển được tính theo khu vực: nội thành (zone 1), các tỉnh lân cận (zone 2) và các tỉnh xa (zone 3). Đơn hàng đạt ngưỡng miễn phí ship sẽ được miễn phí vận chuyển. Chi tiết phí hiển thị khi bạn chọn địa chỉ giao hàng ở bước thanh toán.", "shipping", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Phí vận chuyển như thế nào?", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Fruitables hỗ trợ thanh toán qua SePay QR khi checkout. Sau khi đặt hàng, bạn quét mã QR để chuyển khoản; hệ thống tự xác nhận thanh toán khi nhận được giao dịch.", "payment", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Thanh toán bằng cách nào?", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "Rau củ tươi nên bảo quản trong tủ lạnh (ngăn mát), để trong túi hoặc hộp thoáng khí, tránh để gần trái cây chín. Dùng sớm trong vài ngày để giữ độ tươi ngon tốt nhất.", "product-care", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Bảo quản rau củ tươi như thế nào?", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "Bạn có thể xem giờ làm việc và thông tin liên hệ (điện thoại, email, địa chỉ) trên trang Liên hệ hoặc phần chân trang website. Chúng tôi sẵn sàng hỗ trợ trong khung giờ làm việc đã công bố.", "hours", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Giờ làm việc và liên hệ?", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "Đăng nhập tài khoản, vào mục Lịch sử đơn hàng để xem trạng thái, chi tiết và theo dõi đơn. Bạn cần đăng nhập để xem các đơn gắn với tài khoản của mình.", "order", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Làm sao để kiểm tra đơn hàng?", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "Nếu sản phẩm bị lỗi hoặc không đúng mô tả, vui lòng liên hệ CSKH trong vòng 24 giờ kể từ khi nhận hàng để được hỗ trợ đổi trả. Giữ nguyên bao bì và chụp ảnh minh chứng nếu có.", "return", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Chính sách đổi trả như thế nào?", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}
