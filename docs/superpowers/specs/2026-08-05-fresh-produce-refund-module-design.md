# Thiết kế module khiếu nại hoàn tiền trái cây tươi

- Ngày: 2026-08-05
- Trạng thái: Đã thống nhất trong phiên brainstorming, chờ rà soát bản đặc tả
- Phạm vi: Fruitables, ASP.NET Core MVC, EF Core, SQL Server

## 1. Mục tiêu

Xây dựng luồng khiếu nại hoàn tiền phù hợp với hàng trái cây tươi. Khách có thể báo hàng hư, dập, mốc, không tươi, giao sai hoặc giao thiếu sau khi nhận hàng. Cửa hàng xem xét theo từng sản phẩm, có thể hoàn toàn bộ hoặc một phần số kg.

MVP dùng hoàn tiền thủ công. Hệ thống ghi nhận quyết định, khoản hoàn và mã giao dịch, nhưng không tự chuyển tiền. Hàng đã giao không được nhập lại tồn kho.

## 2. Phạm vi

### Có trong MVP

- Chỉ nhận yêu cầu cho đơn đã chuyển sang `Delivered`.
- Hạn gửi yêu cầu là 24 giờ từ `Order.DeliveredAtUtc`.
- Mỗi đơn chỉ có một yêu cầu.
- Một yêu cầu có thể chứa nhiều sản phẩm trong đơn.
- Số lượng hỗ trợ bước 0,1 kg.
- Khách có thể yêu cầu hoàn toàn bộ hoặc một phần số kg.
- Lý do gồm: hư/dập, mốc, không tươi, giao sai và giao thiếu.
- Lý do hư hỏng cần ít nhất một ảnh; giao sai và giao thiếu cần mô tả, ảnh được khuyến nghị.
- Khách được hủy yêu cầu khi chưa có quyết định.
- Admin có thể yêu cầu bổ sung thông tin một lần.
- Khách có 24 giờ để bổ sung ảnh hoặc mô tả.
- Admin duyệt hoặc từ chối theo từng sản phẩm.
- Admin bắt buộc ghi lý do khi duyệt ít hơn số kg yêu cầu hoặc từ chối.
- Hoàn phí ship chỉ áp dụng khi lỗi thuộc shop hoặc đơn vị vận chuyển và ảnh hưởng toàn bộ đơn.
- Khách xem trạng thái trong chi tiết đơn hàng, không gửi email trong MVP.
- Admin/SuperAdmin xử lý bằng permission hiện có `orders.refund`.

### Không có trong MVP

- Thu hồi hàng vật lý.
- Nhập lại hàng hư vào kho.
- Đổi sản phẩm hoặc giao bù.
- Voucher, store credit hoặc tự động hoàn tiền.
- Tự duyệt theo hạn mức.
- Kháng nghị sau khi từ chối.
- Nhiều yêu cầu trên cùng một đơn.
- Lưu tài khoản ngân hàng của khách.
- Engine chính sách, risk scoring hoặc dashboard cấu hình động.

## 3. Vòng đời nghiệp vụ

### Trạng thái yêu cầu

- `Submitted`: khách vừa gửi.
- `UnderReview`: admin đang xem xét.
- `AwaitingCustomerInfo`: admin yêu cầu bổ sung.
- `AwaitingRefund`: đã có quyết định và chờ admin chuyển tiền.
- `Refunded`: khoản hoàn đã được xác nhận.
- `Rejected`: tất cả sản phẩm bị từ chối.
- `Cancelled`: khách hủy trước khi có quyết định.

Yêu cầu được duyệt một phần vẫn dùng `AwaitingRefund`. Kết quả từng sản phẩm được lưu riêng, không cần thêm trạng thái tổng hợp `PartiallyApproved`. Nếu khách không bổ sung trước hạn 24 giờ, service chuyển yêu cầu sang `Rejected` và ghi lý do hết hạn vào timeline.

### Trạng thái sản phẩm

- `Pending`
- `Approved`
- `Rejected`

Một sản phẩm có thể được duyệt với `ApprovedQuantity` nhỏ hơn `RequestedQuantity`.

### Luồng khách hàng

1. Khách mở chi tiết đơn hàng.
2. Hệ thống kiểm tra đơn đã giao, hạn 24 giờ và chưa có yêu cầu.
3. Khách chọn sản phẩm, số kg, lý do, mô tả và tải ảnh nếu cần.
4. Hệ thống tạo yêu cầu và các dòng sản phẩm trong một giao dịch.
5. Khách theo dõi trạng thái trong trang chi tiết yêu cầu.
6. Nếu admin yêu cầu bổ sung, khách được gửi thêm ảnh hoặc mô tả một lần trong 24 giờ.
7. Sau khi quyết định, khách xem số tiền được duyệt và trạng thái chuyển tiền.

### Luồng admin

1. Admin mở danh sách khiếu nại, lọc theo trạng thái, ngày hoặc mã đơn.
2. Admin xem thông tin đơn, sản phẩm, số kg đã mua, mô tả và ảnh.
3. Admin có thể yêu cầu bổ sung, duyệt từng dòng hoặc từ chối từng dòng.
4. Hệ thống tính tiền hoàn theo số tiền thực trả.
5. Nếu có sản phẩm được duyệt, hệ thống tạo một khoản `Refund` ở trạng thái chờ xử lý và chuyển yêu cầu sang `AwaitingRefund`.
6. Admin chuyển tiền bên ngoài hệ thống, nhập mã giao dịch và xác nhận hoàn tất. Nếu việc chuyển tiền không thành công, khoản hoàn giữ trạng thái `Failed`, yêu cầu vẫn ở `AwaitingRefund` và admin có thể xử lý lại.
7. Hệ thống ghi sự kiện, cập nhật trạng thái yêu cầu và chỉ đổi `Order.PaymentStatus` sang `Refunded` khi tổng tiền hoàn thành bằng toàn bộ tiền đơn.

`Order.Status` vẫn là `Delivered`. Không có thao tác cộng lại `Product.StockQuantity` hoặc `ProductVariant.StockQuantity`.

## 4. Mô hình dữ liệu

Namespace mới: `Fruitables.Models.Returns`.

### `ReturnRequest`

Lưu thông tin cấp yêu cầu:

- `Id`, `ReturnNumber`, `OrderId`, `UserId`.
- `Status`.
- `SubmittedAtUtc`, `ClaimDeadlineAtUtc`.
- `SupplementDeadlineAtUtc`, `SupplementCount`.
- `RequestedAmount`, `ApprovedAmount`, `ApprovedShippingFeeAmount`.
- `CustomerNote`, `AdminNote`.
- `RowVersion`.
- Quan hệ với `Order`, `User`, `ReturnRequestItem`, `ReturnEvidence`, `ReturnEvent` và `Refund`.

Ràng buộc: `OrderId` là duy nhất để bảo đảm mỗi đơn chỉ có một yêu cầu.

### `ReturnRequestItem`

Lưu quyết định theo sản phẩm:

- `ReturnRequestId`, `OrderItemId`.
- `DecisionStatus`.
- `RequestedQuantity`, `ApprovedQuantity` kiểu `decimal`.
- `Reason`, `Description`, `DecisionReason`.
- `RequestedAmount`, `ApprovedAmount`.

Ràng buộc: một `OrderItem` chỉ xuất hiện một lần trong cùng yêu cầu; số kg được duyệt không âm và không vượt số kg yêu cầu.

### `ReturnEvidence`

Lưu ảnh xác minh:

- `ReturnRequestId`, `ReturnRequestItemId` tùy phạm vi ảnh.
- `StorageKey`, tên file gốc, loại file, kích thước.
- `UploadedByUserId`, `UploadedAtUtc`.

Dùng lại `IImageUploadService`. Không tin tên file hoặc loại file do client gửi lên.

### `ReturnEvent`

Lưu timeline:

- `ReturnRequestId`, `ReturnRequestItemId` tùy sự kiện.
- Trạng thái cũ, trạng thái mới, loại sự kiện.
- `ActorUserId`, ghi chú, thời gian.

Các sự kiện gồm gửi yêu cầu, yêu cầu bổ sung, bổ sung thông tin, duyệt, từ chối, hủy, tạo khoản hoàn và hoàn tất khoản hoàn.

### `Refund`

Mỗi yêu cầu chỉ có một khoản hoàn tổng hợp:

- `ReturnRequestId`, `OrderId`.
- `Amount`, `ShippingFeeAmount`.
- `Status`: `Pending`, `Succeeded`, `Failed`.
- `TransactionReference`, `FailureReason`.
- `CreatedByUserId`, `ProcessedByUserId`.
- `CreatedAtUtc`, `ProcessedAtUtc`.

Không lưu thông tin tài khoản ngân hàng. Admin xử lý điểm nhận tiền bên ngoài hệ thống rồi nhập mã giao dịch. Mã giao dịch thành công phải duy nhất.

## 5. Số lượng và tồn kho

Do yêu cầu hỗ trợ 0,1 kg, các trường biểu diễn lượng sản phẩm sẽ chuyển từ `int` sang `decimal` với precision phù hợp trong SQL Server:

- `Product.StockQuantity`.
- `ProductVariant.StockQuantity`.
- `Product.MinOrderQuantity`.
- `CartItem.Quantity`.
- `OrderItem.Quantity`.
- `ComboItem.Quantity`.
- `Coupon.MinQuantity` nếu được dùng để tính tổng lượng sản phẩm mua.
- Các view model, request model và phép tính trong cart, checkout, order, combo, coupon, hủy đơn và hoàn kho.

`CartGroup.Quantity` và `OrderItem.ComboQuantity` vẫn là `int` vì chúng biểu diễn số bundle. Sản phẩm bên trong bundle có thể dùng số kg lẻ.

Sản phẩm có đơn vị `kg` được nhận bước 0,1. Sản phẩm tính theo quả hoặc đơn vị khác chỉ nhận số nguyên. Không dùng `double` hoặc `float`; toàn bộ tính toán dùng `decimal`.

Migration phải bảo toàn dữ liệu số nguyên hiện có bằng cách chuyển đổi `1` thành `1,0`.

## 6. Công thức tiền hoàn

Tiền hoàn dùng số tiền thực trả tại thời điểm đặt hàng, không dùng giá hiện tại.

1. Lấy giá và tổng tiền snapshot trong `OrderItem`.
2. Phân bổ giảm giá cấp đơn theo tỷ lệ giá trị dòng hàng.
3. Tính đơn giá thực trả sau giảm giá.
4. Nhân với `ApprovedQuantity / OrderedQuantity`.
5. Cộng phí ship chỉ khi quyết định hoàn phí ship được bật.
6. Làm tròn đến VNĐ.
7. Không cho tổng tiền hoàn thành công vượt số tiền khách đã thanh toán.

Hoàn một phần giữ `Order.PaymentStatus = Paid`. Chỉ khi tổng khoản hoàn thành bằng toàn bộ `Order.Total` thì chuyển sang `Refunded`. Việc chuyển trạng thái này chỉ được thực hiện trong workflow hoàn tiền, không qua form cập nhật trạng thái thanh toán chung.

## 7. Quy tắc phí vận chuyển

- Chỉ ảnh hưởng một phần đơn: không hoàn phí ship.
- Toàn bộ đơn bị hư, giao sai hoặc không thể sử dụng do shop hoặc đơn vị vận chuyển: có thể hoàn toàn bộ phí ship.
- Lỗi do khách bảo quản hoặc nguyên nhân chưa xác định: không hoàn phí ship.
- Admin có thể điều chỉnh quyết định nhưng phải ghi lý do.

`ApprovedShippingFeeAmount` được snapshot trong `ReturnRequest` và `Refund`, không tính lại theo cấu hình ship hiện tại.

## 8. Kiến trúc ứng dụng

Dùng một service nghiệp vụ chính `IReturnService` và một implementation `ReturnService` cho MVP. Service xử lý kiểm tra điều kiện, tạo yêu cầu, bổ sung thông tin, quyết định admin và ghi nhận hoàn tiền thủ công.

Các điểm tích hợp:

- `ReturnController`: thao tác của khách, kiểm tra ownership.
- `Areas/Admin/Controllers/ReturnController`: hàng đợi, xem xét, quyết định và xác nhận hoàn tiền.
- `ApplicationDbContext`: DbSet và cấu hình quan hệ, index, check constraint.
- `IImageUploadService`: lưu ảnh.
- RBAC hiện có: `orders.refund`.
- `Order` và `OrderItem`: liên kết snapshot giá, trạng thái thanh toán và số lượng.

Không thêm policy engine, refund provider, outbox, email hoặc abstraction cho API chuyển tiền trong MVP.

## 9. Bảo mật, lỗi và đồng thời

- Bắt buộc xác thực và kiểm tra `UserId` của đơn.
- Dùng antiforgery cho mọi POST.
- Kiểm tra hạn 24 giờ ở server, không tin nút ẩn trên giao diện.
- Kiểm tra bước 0,1 kg và số kg còn có thể khiếu nại.
- Unique index chặn yêu cầu trùng.
- `RowVersion` chặn hai admin ghi đè quyết định.
- Tạo quyết định, khoản hoàn và sự kiện trong giao dịch phù hợp.
- Chỉ cho khoản hoàn chuyển sang `Succeeded` một lần.
- Tệp upload phải có loại, kích thước và storage key hợp lệ.
- Lỗi đồng thời trả về thông báo yêu cầu tải lại, không ghi đè dữ liệu mới.
- Hàng hư hoặc giao sai không được tự động cộng lại tồn kho.

## 10. Kiểm thử bắt buộc

### Điều kiện và quyền

- Đơn chưa giao hoặc quá 24 giờ không tạo được yêu cầu.
- Người dùng không xem hoặc sửa được đơn của người khác.
- Đơn đã có yêu cầu không tạo thêm yêu cầu.
- Admin không có `orders.refund` không xử lý được.

### Số lượng và tiền

- Nhận 0,1 kg và từ chối giá trị không đúng bước.
- Không vượt số kg đã mua.
- Duyệt một phần và toàn bộ.
- Phân bổ giảm giá đúng.
- Làm tròn VNĐ ổn định.
- Không hoàn vượt tiền đã thanh toán.
- Phí ship chỉ được hoàn theo quy tắc.

### Vòng đời

- Hủy trước quyết định.
- Yêu cầu bổ sung một lần.
- Hết hạn bổ sung sau 24 giờ.
- Từ chối với lý do bắt buộc.
- Duyệt tạo đúng một khoản hoàn.
- Xác nhận hoàn tiền hai lần không làm tăng số tiền hoàn.
- Đổi `PaymentStatus` đúng khi hoàn đủ.

### Đồng thời và tồn kho

- Hai request đồng thời cho cùng đơn chỉ một request thành công.
- Hai admin quyết định đồng thời không ghi đè nhau.
- Hoàn tiền không thay đổi tồn kho.
- Luồng đặt hàng, hủy đơn và combo vẫn tính đúng sau khi đổi sang `decimal`.

## 11. Tiêu chí hoàn thành

- Khách có thể gửi, xem, hủy và bổ sung một yêu cầu hợp lệ.
- Admin có thể xem xét, duyệt từng dòng, từ chối, quyết định phí ship và xác nhận hoàn thủ công.
- Lịch sử xử lý hiển thị được cho khách và admin.
- Không có hoàn trùng, duyệt trùng hoặc nhập lại hàng hư vào kho.
- Dữ liệu cũ vẫn chạy sau migration số lượng.
- Build và toàn bộ test liên quan đến cart, order, combo, stock và return đều đạt.
