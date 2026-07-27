# Kế hoạch triển khai module trả hàng chuẩn production

> Tài liệu này là checklist triển khai cho project Fruitables. Đánh dấu `- [x]` khi một task hoặc phase đã hoàn thành và được kiểm chứng.

## Mục tiêu tổng quát

Xây dựng module **Khiếu nại chất lượng / Đổi trả sau giao hàng** phù hợp với rau củ quả mau hỏng, hỗ trợ xử lý theo từng sản phẩm, hoàn tiền một phần, giao bù, bằng chứng, audit và kiểm soát tồn kho an toàn.

Sau khi hoàn thành toàn bộ kế hoạch, Fruitables sẽ có một quy trình sau bán hàng khép kín như sau:

1. Khách hàng mở đơn đã giao và xem sản phẩm nào còn đủ điều kiện khiếu nại theo chính sách đang áp dụng.
2. Khách chọn từng sản phẩm, số lượng bị ảnh hưởng, lý do, phương án mong muốn và tải ảnh hoặc video làm bằng chứng.
3. Hệ thống kiểm tra quyền sở hữu đơn hàng, thời hạn, số lượng còn có thể claim, yêu cầu bằng chứng và mức tiền hoàn tối đa.
4. Nhân viên CSKH tiếp nhận yêu cầu trong một hàng đợi có SLA, xem bằng chứng, yêu cầu bổ sung thông tin, duyệt toàn phần, duyệt một phần hoặc từ chối.
5. Yêu cầu được giải quyết bằng hoàn tiền, giao sản phẩm thay thế, store credit hoặc một quyết định khác được policy cho phép.
6. Finance xử lý hoàn tiền có idempotency, maker-checker, transaction reference và reconciliation; hệ thống chỉ ghi nhận hoàn tiền thành công sau khi giao dịch thật sự hoàn tất.
7. Khách theo dõi timeline xử lý và nhận thông báo khi yêu cầu thay đổi trạng thái, cần bổ sung bằng chứng, được duyệt, bị từ chối hoặc đã hoàn tất.
8. Kho ghi nhận disposition của hàng lỗi như không thu hồi, quarantine, tiêu hủy, trả nhà cung cấp hoặc restock sau QA; hàng tươi đã giao không tự động quay lại tồn bán được.
9. Khi dùng phương án giao bù, hệ thống tạo replacement order liên kết với đơn gốc và trừ tồn kho atomically như một fulfillment thực tế.
10. Quản trị viên theo dõi tỷ lệ khiếu nại, số tiền hoàn, nguyên nhân lỗi, SLA, chi phí giao bù, waste và sản phẩm, nhà cung cấp hoặc lô hàng có rủi ro cao.

### Trải nghiệm khách hàng sau khi hoàn thành

- Tạo yêu cầu hỗ trợ trực tiếp từ lịch sử đơn hàng.
- Khiếu nại một phần đơn hàng thay vì buộc trả toàn bộ đơn.
- Biết rõ deadline, bằng chứng cần cung cấp và số lượng còn đủ điều kiện.
- Bổ sung bằng chứng hoặc hủy yêu cầu khi workflow cho phép.
- Theo dõi timeline và kết quả xử lý minh bạch.
- Nhận hoàn tiền một phần, toàn phần, store credit hoặc đơn giao bù theo quyết định được duyệt.
- Không phải gửi ngược rau củ quả hư hỏng về cửa hàng trong trường hợp policy không yêu cầu.

### Khả năng vận hành của CSKH và quản trị viên

- Quản lý tất cả yêu cầu trên một queue có tìm kiếm, bộ lọc và cảnh báo SLA.
- Xử lý theo từng item và từng quantity, hỗ trợ duyệt một phần.
- Áp dụng policy theo product, category hoặc policy mặc định và snapshot policy tại thời điểm submit.
- Kiểm soát override bằng RBAC, reason bắt buộc và supervisor approval.
- Có audit append-only cho mọi transition, quyết định, refund và inventory disposition.
- Phát hiện concurrency conflict khi nhiều nhân viên cùng xử lý một yêu cầu.
- Xem risk signals để chuyển các trường hợp bất thường sang manual review.

### Khả năng tài chính sau khi hoàn thành

- Tách refund transaction khỏi trạng thái vận chuyển của đơn hàng.
- Hỗ trợ `PartiallyRefunded` và nhiều lần hoàn tiền trên cùng một đơn.
- Không hoàn vượt số tiền khách thực trả sau coupon, promotion và combo discount.
- Hỗ trợ quy trình hoàn tiền thủ công có kiểm soát và khả năng tích hợp payout provider chính thức sau này.
- Có idempotency, retry, reconciliation và báo cáo mismatch.
- Tính net revenue từ refund thành công theo item thay vì trừ toàn bộ order.

### Khả năng kho và chất lượng sau khi hoàn thành

- Không tự động tăng `Product.StockQuantity` hoặc `ProductVariant.StockQuantity` khi hàng tươi được khiếu nại.
- Theo dõi quarantine, waste, supplier return, donation và restock sau QA.
- Replacement order trừ tồn kho an toàn và không được tính như doanh thu mới.
- Khi Phase 3 hoàn thành, quản lý tồn kho theo lot, hạn sử dụng và FEFO.
- Truy ngược return item về lot và nhà cung cấp đã fulfill sản phẩm.
- Tìm các đơn bị ảnh hưởng khi một lot cần quarantine hoặc recall.

### Trạng thái kỹ thuật mong muốn

- Module hoạt động trong modular monolith hiện tại và dùng chung transaction SQL Server.
- State machine, policy evaluation và refund calculation được tập trung trong service layer.
- Mọi thao tác nhạy cảm có RBAC, audit, antiforgery, ownership validation và optimistic concurrency.
- Bằng chứng được lưu private, kiểm tra loại file và malware trước khi sử dụng.
- Notification được phát qua outbox sau khi transaction commit và có retry.
- Background workers xử lý SLA, refund reconciliation, evidence expiry và outbox backlog.
- Hệ thống có structured logs, metrics, health checks, alert và runbook vận hành.
- Các invariant về quantity, refund amount và inventory được bảo vệ bằng validation, transaction, database constraints và integration tests.

## Kiến trúc mục tiêu

```text
Order
  └── ReturnRequest
       ├── ReturnRequestItems
       ├── ReturnEvidence
       ├── ReturnEvents
       ├── Refunds
       ├── ReplacementOrder
       └── InventoryDispositions
```

Module được triển khai dưới dạng **modular monolith** trong ứng dụng ASP.NET Core MVC hiện tại, dùng chung SQL Server và EF Core transaction. Không tách microservice trong phạm vi kế hoạch này.

## Quyết định nghiệp vụ bắt buộc

- Đơn đã giao vẫn giữ `OrderStatus.Delivered`; trạng thái khiếu nại nằm trong `ReturnRequest`.
- Không dùng `OrderStatus.Returned` làm nguồn dữ liệu chính cho module mới.
- Không tự động cộng lại tồn kho khi khách khiếu nại hoặc hoàn tiền hàng tươi.
- Mặc định không yêu cầu khách gửi ngược rau củ quả về cửa hàng.
- Hỗ trợ khiếu nại và hoàn tiền theo từng `OrderItem`.
- `Refund` là nguồn dữ liệu tài chính chuẩn; không đánh dấu hoàn tiền thành công trước khi tiền thực sự được xử lý.
- Mọi thời điểm mới được lưu theo UTC và lấy từ `TimeProvider`.
- Giai đoạn 1 tiếp tục dùng số lượng nguyên vì `OrderItem.Quantity` hiện là `int`. Bán hàng theo cân thực tế được xử lý trong Phase 3.
- SePay hiện chỉ đang xác nhận giao dịch tiền vào. Không giả định SePay hỗ trợ payout hoặc hoàn tiền tự động.

---

# Tổng quan tiến độ

- [x] **Phase 0 — Chốt chính sách và khóa các đường xử lý nguy hiểm hiện tại**
- [x] **Phase 1 — MVP an toàn: tạo yêu cầu, duyệt theo item và hoàn tiền thủ công**
- [ ] **Phase 2 — Production: reliability, security, automation, replacement và analytics**
- [ ] **Phase 3 — Kho thực phẩm tươi: lot, hạn sử dụng, FEFO và truy xuất nguồn gốc**
- [ ] **Release Acceptance — Toàn bộ tiêu chí production đã đạt**

---

# Phase 0 — Chốt chính sách và khóa logic nguy hiểm

## Mục tiêu Phase 0

Ngăn hệ thống hiện tại tự động hoàn kho hoặc đánh dấu hoàn tiền sai, đồng thời chốt policy trước khi tạo schema.

## Task 0.1 — Chốt chính sách đổi trả

> Baseline dưới đây được chốt để triển khai kỹ thuật. Product owner phải review lại ngôn ngữ pháp lý và điều khoản thương mại trước khi mở production.

- [x] Xác nhận danh sách lý do khiếu nại chuẩn:
  - `DamagedOrBruised`: dập, vỡ hoặc hư hỏng vật lý.
  - `SpoiledOrMoldy`: úng, thối hoặc mốc.
  - `TemperatureIssue`: không đảm bảo nhiệt độ bảo quản.
  - `WiltedOrNotFresh`: héo hoặc không còn tươi.
  - `WrongItem`: giao sai sản phẩm hoặc biến thể.
  - `MissingItem`: thiếu sản phẩm.
  - `UnderweightOrShortQuantity`: thiếu cân hoặc thiếu số lượng.
  - `LateDeliveryCausedDamage`: giao trễ làm giảm chất lượng.
  - `FoodSafetyConcern`: nghi ngờ an toàn thực phẩm.
  - `ChangeOfMind`: khách đổi ý, mặc định không đủ điều kiện với thực phẩm tươi.
  - `Other`: lý do khác, bắt buộc mô tả và manual review.
- [x] Chốt cửa sổ khiếu nại cho rau lá, rau thơm, quả mọng và nấm: **12 giờ** từ `DeliveredAtUtc`.
- [x] Chốt cửa sổ khiếu nại cho trái cây và củ quả thông thường: **24 giờ** từ `DeliveredAtUtc`.
- [x] Chốt cửa sổ khiếu nại cho hàng khô hoặc hàng đóng gói nguyên seal: **7 ngày** từ `DeliveredAtUtc`.
- [x] Chốt evidence bắt buộc cho `DamagedOrBruised`, `SpoiledOrMoldy`, `TemperatureIssue`, `WiltedOrNotFresh`, `WrongItem`, `MissingItem`, `UnderweightOrShortQuantity`, `LateDeliveryCausedDamage` và `FoodSafetyConcern`.
- [x] Chốt giới hạn upload: tối đa **5 file**; ảnh JPEG, PNG hoặc WebP tối đa **10 MB/file**; tối đa một video MP4 **30 MB**; tổng request tối đa **40 MB**.
- [x] Chốt hoàn shipping fee chỉ khi toàn bộ đơn không sử dụng được do lỗi cửa hàng hoặc vận chuyển, giao sai toàn bộ, thiếu toàn bộ hoặc đơn đã thu tiền nhưng không giao được. Khiếu nại một phần không hoàn shipping fee mặc định.
- [x] Chốt resolution: hoàn một phần theo item, hoàn toàn bộ phần đủ điều kiện, giao bù, store credit hoặc từ chối. Physical return không bắt buộc với rau củ hỏng; hàng nguyên seal có thể được yêu cầu thu hồi theo policy.
- [x] Chốt auto-approve **tắt mặc định** trong Phase 1. Nếu bật ở Phase 2, hard cap ban đầu là **100.000₫**, phải có evidence sạch, không có risk flag và không thuộc `FoodSafetyConcern`.
- [x] Chốt supervisor approval khi tổng resolution từ **500.000₫**, khi override policy hoặc khi chọn `Restocked` cho hàng đã rời kho.
- [x] Chốt thời gian bổ sung evidence: **24 giờ** từ lúc CSKH yêu cầu; hết hạn chuyển `Expired` nếu khách không bổ sung.
- [x] Chốt retention: evidence **90 ngày** sau khi request resolved; dữ liệu tài khoản nhận tiền **30 ngày** sau reconciliation; audit và refund transaction giữ theo chính sách kế toán đã được doanh nghiệp phê duyệt.
- [x] Đưa nội dung policy baseline vào tài liệu vận hành và chuẩn bị nội dung hiển thị cho khách hàng.

### Nội dung policy dự kiến hiển thị cho khách hàng

> Fruitables hỗ trợ khi sản phẩm bị dập, hỏng, mốc, giao sai, giao thiếu, thiếu cân hoặc không bảo đảm chất lượng khi giao. Vui lòng gửi yêu cầu trong 12 giờ đối với nhóm rau lá, rau thơm, quả mọng và nấm; trong 24 giờ đối với trái cây và củ quả thông thường; hoặc trong 7 ngày đối với hàng khô còn nguyên seal. Yêu cầu cần có ảnh hoặc video thể hiện sản phẩm, bao bì và tem giao hàng. Tùy kết quả kiểm tra, Fruitables có thể hoàn tiền cho phần bị ảnh hưởng, giao bù hoặc cấp store credit. Thực phẩm tươi hư hỏng thường không cần gửi ngược về cửa hàng. Trường hợp đổi ý hoặc bảo quản không đúng hướng dẫn không thuộc phạm vi hỗ trợ mặc định.

## Task 0.2 — Viết regression test cho hành vi tồn kho hiện tại

**Files dự kiến:**

- Modify: `Tests/OrderAdminServiceTests.cs`
- Modify: `Tests/OrderVariantStockTests.cs`

- [x] Viết test chứng minh đơn `Delivered` không được hủy bằng luồng hủy đơn thông thường.
- [x] Viết test chứng minh chuyển trạng thái legacy sang `Returned` không được tự động cộng `Product.StockQuantity`.
- [x] Viết test tương tự cho `ProductVariant.StockQuantity`.
- [x] Viết test chứng minh đơn `Pending` bị hủy vẫn được hoàn lại tồn kho vì hàng chưa giao.
- [x] Viết test chứng minh không thể chuyển `PaymentStatus` sang `Refunded` chỉ bằng thay đổi trạng thái đơn hàng.
- [x] Chạy test trước khi sửa code: xác nhận 4 regression tests thất bại đúng hành vi cần khóa; sau khi sửa, 15 order/stock tests liên quan đều pass.

## Task 0.3 — Khóa đường cập nhật trạng thái legacy

**Files dự kiến:**

- Modify: `Services/OrderAdminService.cs`
- Modify: `ViewModels/OrderViewModels.cs`
- Modify: `Areas/Admin/Controllers/OrderController.cs`
- Modify: `Areas/Admin/Views/Order/Detail.cshtml`
- Modify: `Controllers/OrderHistoryController.cs`

- [x] Xóa quyền chuyển trực tiếp từ `Delivered` sang `Cancelled` trong luồng quản lý đơn hàng.
- [x] Không gọi `RestoreStockForOrder` khi đơn đã giao.
- [x] Không gọi `RestoreStockForOrder` khi trạng thái đích là `Returned`.
- [x] Ẩn `Returned` khỏi UI cập nhật trạng thái đơn mới.
- [x] Giữ nguyên numeric value của enum `OrderStatus.Returned` để không làm hỏng dữ liệu cũ.
- [x] Đánh dấu `Returned` là trạng thái legacy trong code và chỉ cho phép đọc dữ liệu lịch sử.
- [x] Khóa các đường cập nhật order legacy sau khi hàng đã xuất kho; Phase 1 sẽ nối các action này sang `ReturnRequest`.
- [x] Không cho admin chuyển trực tiếp `PaymentStatus.Paid` thành `Refunded` ngoài refund workflow.
- [x] Xóa các generic status mutation methods không có validation khỏi `IOrderService` và `IOrderRepository` để không còn đường bypass state machine.
- [x] Cập nhật thông báo lỗi và admin UI để hướng nhân viên sang module Khiếu nại & đổi trả.

## Task 0.4 — Chuẩn hóa nguyên tắc thời gian cho module mới

- [x] Quy định tất cả field mới dùng hậu tố `Utc` hoặc dùng `DateTimeOffset` nhất quán.
- [x] Dùng `TimeProvider` đã đăng ký trong `Program.cs`; không gọi trực tiếp `DateTime.UtcNow.AddHours(7)` trong module mới.
- [x] Chuyển múi giờ `Asia/Ho_Chi_Minh` ở View hoặc presentation layer.
- [x] Lập danh sách các field thời gian cũ đang lưu lẫn UTC và UTC+7 để xử lý khi backfill `DeliveredAtUtc`.
- [x] Không tự động chuyển đổi dữ liệu lịch sử nếu chưa xác định chắc múi giờ của từng nguồn.

### Inventory timestamp cần xử lý trước khi backfill `DeliveredAtUtc`

| Nguồn | Cách ghi hiện tại | Rủi ro |
|---|---|---|
| `Order.CreatedAt` | Default `DateTime.UtcNow.AddHours(7)` | Giá trị local nhưng không có timezone metadata. |
| `OrderStatusHistory.CreatedAt` entity default | `DateTime.UtcNow.AddHours(7)` | Có thể là UTC+7 nếu caller không set. |
| `OrderAdminService` status history | Explicit `DateTime.UtcNow` | Cùng column nhưng khác quy ước với entity default. |
| `OrderLogService` status history | Explicit `DateTime.UtcNow` | Không thể phân biệt tự động với row dùng default chỉ từ kiểu `datetime2`. |
| `OrderRepository` status history | Explicit `DateTime.UtcNow` | Cần giữ UTC khi xây `DeliveredAtUtc`. |
| `OrderNote.CreatedAt` | Default `DateTime.UtcNow.AddHours(7)` | Không dùng để tính SLA nhưng cần chuẩn hóa presentation về sau. |

**Quy tắc backfill:** chỉ dùng status history có nguồn và timezone xác định được; các order legacy không chắc chắn phải vào exception report/manual review, không tự động cộng hoặc trừ 7 giờ hàng loạt.

## Task 0.5 — Kiểm chứng Phase 0

- [x] Chạy `dotnet test Tests/Fruitables.Tests.csproj --no-restore`: **394/394 tests pass**.
- [x] Chạy `dotnet build Fruitables.csproj --no-restore`: **0 warnings, 0 errors**.
- [x] Xác nhận hủy đơn `Pending` vẫn hoàn kho chính xác bằng SQLite integration-style test.
- [x] Xác nhận đơn `Delivered` và legacy `Returned` không còn đường service nào tự động hoàn product hoặc variant stock.
- [x] Xác nhận admin không thể đánh dấu hoàn tiền thành công bằng form hoặc service trạng thái thanh toán.
- [x] Commit Phase 0 bằng commit riêng, có mô tả thay đổi nghiệp vụ.
- [x] **Phase 0 hoàn thành.**

---

# Phase 1 — MVP an toàn

## Mục tiêu Phase 1

Khách hàng có thể gửi yêu cầu theo từng sản phẩm; nhân viên có thể yêu cầu thêm bằng chứng, duyệt toàn phần, duyệt một phần hoặc từ chối; finance có thể ghi nhận hoàn tiền thủ công; hàng tươi không quay lại tồn kho bán được.

## Task 1.1 — Tạo domain enums và entities

**Files dự kiến:**

- Create: `Models/Returns/ReturnRequest.cs`
- Create: `Models/Returns/ReturnRequestItem.cs`
- Create: `Models/Returns/ReturnEvidence.cs`
- Create: `Models/Returns/ReturnEvent.cs`
- Create: `Models/Returns/ReturnPolicy.cs`
- Create: `Models/Returns/Refund.cs`
- Create: `Models/Returns/InventoryDisposition.cs`
- Create: `Models/Returns/ReturnEnums.cs`
- Modify: `Models/Order.cs`

- [x] Tạo `ReturnRequestStatus` gồm:
  - `Submitted`.
  - `AwaitingEvidence`.
  - `UnderReview`.
  - `Approved`.
  - `PartiallyApproved`.
  - `Rejected`.
  - `ResolutionPending`.
  - `ResolutionFailed`.
  - `Resolved`.
  - `Cancelled`.
  - `Expired`.
- [x] Tạo `ReturnReasonCode` theo policy của Task 0.1.
- [x] Tạo `ReturnResolutionType` gồm `None`, `PartialRefund`, `FullRefund`, `Replacement`, `StoreCredit` và `Reject`.
- [x] Tạo `RefundStatus` gồm `Pending`, `AwaitingDestination`, `AwaitingApproval`, `Processing`, `Succeeded`, `Failed` và `Cancelled`.
- [x] Tạo `RefundMethod` gồm `ManualBankTransfer`, `OriginalPaymentMethod` và `StoreCredit`.
- [x] Tạo `InventoryDispositionType` gồm `NotReturned`, `Quarantined`, `Discarded`, `Donated`, `ReturnedToSupplier` và `Restocked`.
- [x] Tạo `EvidenceScanStatus` gồm `Pending`, `Clean`, `Rejected` và `ScanFailed`.
- [x] Tạo `ReturnRequest` với return number, order, user, status, resolution, policy version, SLA timestamps, reviewer, notes và `RowVersion`.
- [x] Tạo `ReturnRequestItem` liên kết đúng `OrderItem`, lưu requested quantity, approved quantity, reason, amount snapshot và approved amount.
- [x] Tạo `ReturnEvidence` lưu storage key, MIME type, size, checksum, scan status và upload timestamp.
- [x] Tạo `ReturnEvent` append-only để lưu mọi transition và quyết định.
- [x] Tạo `ReturnPolicy` hỗ trợ scope mặc định, category hoặc product; reason; window hours; evidence requirement; resolution flags; thời gian hiệu lực và version.
- [x] Tạo `Refund` lưu amount, method, status, idempotency key, reference, failure reason và processed timestamp.
- [x] Tạo `InventoryDisposition` liên kết return item, quantity, disposition, inspector và notes.
- [x] Thêm `DeliveredAtUtc` nullable vào `Order`.
- [x] Thêm navigation từ `Order` đến danh sách `ReturnRequests`.
- [x] Không thêm navigation khiến xóa order cascade làm mất audit tài chính ngoài ý muốn.

## Task 1.2 — Cấu hình EF Core và database constraints

**Files dự kiến:**

- Modify: `Data/ApplicationDbContext.cs`
- Create: `Migrations/*_AddReturnClaimsFoundation.cs`
- Modify: `Migrations/ApplicationDbContextModelSnapshot.cs`

- [x] Thêm `DbSet` cho tất cả entity của module.
- [x] Tạo unique index cho `ReturnRequest.ReturnNumber`.
- [x] Tạo unique index cho `ReturnRequest.IdempotencyKey` theo user hoặc global theo contract đã chọn.
- [x] Tạo index `(OrderId, Status)` cho truy vấn yêu cầu đang hoạt động.
- [x] Tạo index `(UserId, SubmittedAtUtc)` cho lịch sử khách hàng.
- [x] Tạo index `(Status, ReviewDueAtUtc)` cho hàng chờ admin và SLA worker.
- [x] Tạo index `(ReturnRequestId, OrderItemId)` cho return lines.
- [x] Tạo unique index cho `Refund.IdempotencyKey`.
- [x] Tạo filtered unique index cho provider reference khi reference khác null.
- [x] Tạo index `(Status, CreatedAtUtc)` cho refund worker.
- [x] Cấu hình money column bằng decimal có precision nhất quán với `Order.Total`.
- [x] Tạo check constraint đảm bảo requested quantity lớn hơn 0.
- [x] Tạo check constraint đảm bảo approved quantity không âm và không lớn hơn requested quantity.
- [x] Tạo check constraint đảm bảo refund amount lớn hơn 0.
- [x] Dùng `DeleteBehavior.Restrict` hoặc `NoAction` cho dữ liệu quyết định, refund và audit.
- [x] Dùng cascade chỉ cho evidence chưa có giá trị tài chính khi xóa draft chưa submit, nếu luồng draft được hỗ trợ.
- [x] Tạo migration `AddReturnClaimsFoundation`.
- [x] Review SQL migration trước khi apply.
- [x] Apply migration trên database local.
- [x] Xác nhận migration rollback được trên database test không chứa giao dịch production.

## Task 1.3 — Ghi nhận thời điểm giao hàng chính xác

**Files dự kiến:**

- Modify: `Services/OrderAdminService.cs`
- Modify: `Services/OrderLogService.cs`
- Modify: các integration nhận trạng thái giao hàng nếu có
- Create: script hoặc command backfill được review riêng

- [x] Khi order chuyển sang `Delivered`, set `DeliveredAtUtc` trong cùng transaction với status change.
- [x] Không ghi đè `DeliveredAtUtc` nếu status update được gửi lặp lại.
- [x] Không cho admin tự sửa `DeliveredAtUtc` từ form thông thường.
- [x] Xây script báo cáo đơn `Delivered` nhưng thiếu `DeliveredAtUtc`.
- [x] Backfill từ lần đầu `OrderStatusHistory.NewStatus == Delivered` chỉ khi xác định được timestamp hợp lệ.
- [x] Đưa đơn legacy không xác định được thời gian vào diện manual review, không tự mở vô hạn thời gian trả hàng.
- [x] Test thời điểm giao hàng được ghi một lần và dùng UTC.

## Task 1.4 — Xây policy engine và eligibility service

**Files dự kiến:**

- Create: `Services/Interfaces/IReturnPolicyService.cs`
- Create: `Services/Interfaces/IReturnEligibilityService.cs`
- Create: `Services/Returns/ReturnPolicyService.cs`
- Create: `Services/Returns/ReturnEligibilityService.cs`
- Create: `ViewModels/Returns/ReturnEligibilityViewModels.cs`
- Modify: `Program.cs`

- [x] Implement thứ tự ưu tiên policy: product trước, category sau, cuối cùng là default.
- [x] Chỉ lấy policy đang active và nằm trong thời gian hiệu lực.
- [x] Tính deadline từ `DeliveredAtUtc` bằng `TimeProvider`.
- [x] Chỉ cho tạo yêu cầu từ order thuộc user hiện tại.
- [x] Chỉ cho tạo yêu cầu khi order đã `Delivered`.
- [x] Từ chối order `Pending`, `Processing`, `Shipped` hoặc `Cancelled`.
- [x] Kiểm tra reason có được policy hỗ trợ hay không.
- [x] Kiểm tra evidence requirement theo reason và policy.
- [x] Tính remaining claimable quantity bằng ordered quantity trừ quantity đã được approve hoặc đang xử lý.
- [x] Không cho nhiều request đồng thời claim vượt số lượng đã mua.
- [x] Trả về lý do không đủ điều kiện rõ ràng cho từng order item.
- [x] Snapshot policy id, version, deadline và rule quan trọng vào return item khi submit.
- [x] Đăng ký services trong `Program.cs`.

## Task 1.5 — Xây bộ tính số tiền hoàn

**Files dự kiến:**

- Create: `Services/Interfaces/IRefundAmountCalculator.cs`
- Create: `Services/Returns/RefundAmountCalculator.cs`
- Create: `Models/Returns/RefundCalculationResult.cs`

- [x] Dùng giá trị snapshot trong `OrderItem`, không dùng giá sản phẩm hiện tại.
- [x] Tính trên `OrderItem.Total` sau product promotion và combo discount.
- [x] Phân bổ `Order.Discount` xuống các order item theo tỷ lệ giá trị.
- [x] Dùng thuật toán làm tròn xác định, phân bổ phần dư theo `OrderItem.Id` để tổng allocation đúng bằng `Order.Discount`.
- [x] Tính refundable amount theo approved quantity.
- [x] Hỗ trợ item thuộc combo mà không tự động hoàn toàn bộ combo.
- [x] Trừ các refund `Succeeded` trước đó của cùng order item.
- [x] Không trừ refund `Failed` hoặc `Cancelled`.
- [x] Không cho tổng refund thành công vượt tổng tiền khách đã trả.
- [x] Chỉ hoàn shipping fee khi quyết định có cờ merchant fault và toàn bộ điều kiện policy được thỏa mãn.
- [x] Lưu `NetPaidAmountSnapshot`, `RequestedAmount` và `ApprovedAmount` trên return item để audit.
- [x] Test các trường hợp coupon, combo discount, partial quantity, rounding và nhiều refund liên tiếp.

## Task 1.6 — Xây state machine và ReturnService

**Files dự kiến:**

- Create: `Services/Interfaces/IReturnService.cs`
- Create: `Services/Returns/ReturnService.cs`
- Create: `Models/Returns/ReturnResult.cs`
- Create: `ViewModels/Returns/ReturnRequestViewModels.cs`
- Modify: `Program.cs`

- [x] Định nghĩa transition matrix tập trung, không rải điều kiện trong controller.
- [x] Cho phép `Submitted -> AwaitingEvidence`, `UnderReview` hoặc `Cancelled`.
- [x] Cho phép `AwaitingEvidence -> UnderReview`, `Expired` hoặc `Cancelled`.
- [x] Cho phép `UnderReview -> Approved`, `PartiallyApproved` hoặc `Rejected`.
- [x] Cho phép `Approved/PartiallyApproved -> ResolutionPending`.
- [x] Cho phép `ResolutionPending -> Resolved` hoặc `ResolutionFailed`.
- [x] Cho phép retry `ResolutionFailed -> ResolutionPending` với quyền phù hợp.
- [x] Không cho sửa requested items sau khi request đã submit.
- [x] Cho phép thêm evidence khi status là `AwaitingEvidence`.
- [x] Ghi `ReturnEvent` trong cùng transaction với mỗi transition.
- [x] Dùng `RowVersion` để phát hiện hai admin xử lý cùng yêu cầu.
- [x] Dùng transaction khi submit để kiểm tra lại remaining quantity và tạo request atomically.
- [x] Dùng isolation level hoặc conditional update phù hợp để hai request đồng thời không claim vượt quantity.
- [x] Hỗ trợ idempotency key khi khách submit lại do double click hoặc retry mạng.
- [x] Đăng ký `IReturnService` trong `Program.cs`.

## Task 1.7 — Upload và bảo vệ bằng chứng bản MVP

**Files dự kiến:**

- Create: `Services/Interfaces/IReturnEvidenceService.cs`
- Create: `Services/Returns/ReturnEvidenceService.cs`
- Create: `Controllers/ReturnEvidenceController.cs`
- Create directory at runtime: `App_Data/ReturnEvidence`
- Modify: `Program.cs`

- [x] Lưu file dưới `App_Data/ReturnEvidence`, không lưu trong `wwwroot`.
- [x] Sinh storage key ngẫu nhiên; không dùng tên file khách gửi làm đường dẫn.
- [x] Chuẩn hóa và lưu original file name chỉ để hiển thị audit.
- [x] Giới hạn số lượng file trên request và trên item.
- [x] Giới hạn dung lượng từng file và tổng dung lượng request.
- [x] Allowlist MIME type và extension theo policy đã chốt.
- [x] Kiểm tra file signature, không chỉ tin `Content-Type` từ browser.
- [x] Tính SHA-256 checksum.
- [x] Chặn path traversal và tên file nguy hiểm.
- [x] Chỉ owner, admin có permission hoặc worker được đọc evidence.
- [x] Stream file qua controller có authorization; không expose physical path.
- [x] Thêm cache header phù hợp để dữ liệu riêng tư không bị public cache.
- [x] Gắn `EvidenceScanStatus.Pending`; Phase 2 sẽ tích hợp malware scan.

## Task 1.8 — Xây UI khách hàng

**Files dự kiến:**

- Create: `Controllers/ReturnController.cs`
- Create: `Views/Return/Create.cshtml`
- Create: `Views/Return/Details.cshtml`
- Create: `Views/Return/Index.cshtml`
- Create: `Views/Return/_EligibleItemRow.cshtml`
- Create: `Views/Return/_StatusTimeline.cshtml`
- Modify: `Views/OrderHistory/Details.cshtml`
- Modify: `Views/OrderHistory/Index.cshtml`

- [x] Thêm nút “Yêu cầu hỗ trợ” ở đơn đủ điều kiện.
- [x] Chỉ hiển thị item còn claimable quantity.
- [x] Cho khách chọn item, quantity, reason, mô tả và resolution mong muốn.
- [x] Hiển thị deadline khiếu nại rõ ràng.
- [x] Hiển thị yêu cầu bằng chứng theo reason trước khi submit.
- [x] Validate cả client và server; server là nguồn quyết định cuối cùng.
- [x] Dùng antiforgery token cho tất cả POST.
- [x] Không bind `UserId`, `ApprovedAmount`, `Status` hoặc admin fields từ form khách hàng.
- [x] Tạo idempotency key cho form submit.
- [x] Hiển thị mã yêu cầu và timeline trạng thái.
- [x] Cho khách bổ sung evidence khi `AwaitingEvidence`.
- [x] Cho khách hủy khi request chưa được duyệt và chưa có resolution đang xử lý.
- [x] Không lộ internal admin notes cho khách.
- [x] Hiển thị lý do từ chối hoặc số tiền được duyệt bằng ngôn ngữ rõ ràng.
- [x] Bổ sung trạng thái empty, loading, validation error và file upload error.

## Task 1.9 — Xây admin queue và màn hình duyệt

**Files dự kiến:**

- Create: `Areas/Admin/Controllers/ReturnController.cs`
- Create: `Areas/Admin/Views/Return/Index.cshtml`
- Create: `Areas/Admin/Views/Return/Detail.cshtml`
- Create: `Areas/Admin/Views/Return/_ReturnQueue.cshtml`
- Create: `Areas/Admin/Views/Return/_DecisionForm.cshtml`
- Create: `Areas/Admin/Views/Return/_EvidenceGallery.cshtml`
- Modify: `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`

- [x] Tạo queue lọc theo status, reason, ngày tạo, SLA, order number và customer.
- [x] Sắp xếp ưu tiên request sắp quá SLA.
- [x] Hiển thị order snapshot, shipping snapshot và payment method.
- [x] Hiển thị từng item với ordered quantity, claimed quantity, prior approved quantity và refundable cap.
- [x] Hiển thị evidence bằng URL được authorization.
- [x] Cho admin yêu cầu bổ sung bằng chứng và đặt deadline.
- [x] Cho admin duyệt toàn phần hoặc từng phần theo item.
- [x] Bắt buộc reason khi duyệt khác requested quantity hoặc requested amount.
- [x] Bắt buộc reason khi từ chối.
- [x] Không cho admin nhập approved amount vượt calculator cap.
- [x] Server luôn tính lại amount; không tin hidden input từ browser.
- [x] Hiển thị conflict message khi `RowVersion` thay đổi.
- [x] Ghi đầy đủ `ReturnEvent` cho mọi quyết định.
- [x] Không cho xóa return request đã submit.

## Task 1.10 — Thêm RBAC cho module

**Files dự kiến:**

- Create migration seed permissions hoặc cập nhật migration RBAC phù hợp
- Modify: `Areas/Admin/Controllers/ReturnController.cs`
- Modify: role/permission admin views nếu cần

- [x] Thêm permission `returns.view`.
- [x] Thêm permission `returns.review`.
- [x] Thêm permission `returns.approve`.
- [x] Thêm permission `returns.reject`.
- [x] Thêm permission `returns.refund`.
- [x] Thêm permission `returns.override_policy`.
- [x] Gán permission mặc định cho SuperAdmin.
- [x] Chốt permission mặc định cho Admin và nhân viên CSKH.
- [x] Dùng `[RequirePermission]` trên từng admin action nhạy cảm.
- [x] Viết test user thiếu permission nhận `Forbid`.
- [x] Viết test customer không truy cập được admin return routes.

## Task 1.11 — Hoàn tiền thủ công có kiểm soát

**Files dự kiến:**

- Create: `Services/Interfaces/IRefundService.cs`
- Create: `Services/Returns/RefundService.cs`
- Create: `ViewModels/Returns/RefundViewModels.cs`
- Modify: `Areas/Admin/Controllers/ReturnController.cs`
- Modify: `Areas/Admin/Views/Return/Detail.cshtml`
- Modify: `Models/Order.cs`

- [x] Bổ sung `PaymentStatus.PartiallyRefunded` nhưng không thay đổi numeric value của các enum cũ.
- [x] Chỉ tạo refund từ return request đã `Approved` hoặc `PartiallyApproved`.
- [x] Tạo refund `Pending` bằng idempotency key.
- [x] Không cho tạo refund mới nếu số tiền sẽ vượt refundable remaining.
- [x] Với refund thủ công, yêu cầu finance nhập transaction reference và bằng chứng chuyển tiền.
- [x] Chỉ chuyển refund sang `Succeeded` sau khi finance xác nhận giao dịch đã hoàn tất.
- [x] Không cho cùng một admin vừa approve refund giá trị vượt ngưỡng vừa xác nhận thành công nếu policy yêu cầu maker-checker.
- [x] Khi refund thành công một phần, cập nhật projection `Order.PaymentStatus = PartiallyRefunded`.
- [x] Khi tổng refund thành công bằng tổng số tiền đã thanh toán, cập nhật projection `Order.PaymentStatus = Refunded`.
- [x] Không thay đổi `Order.Status` khi refund thành công.
- [x] Ghi `ReturnEvent` và refund audit trong cùng transaction.
- [x] Lưu reference có unique constraint để tránh ghi nhận một giao dịch hai lần.
- [x] Mask reference nhạy cảm trên UI khách hàng.

## Task 1.12 — Xử lý disposition mà không hoàn kho

**Files dự kiến:**

- Create: `Services/Interfaces/IReturnDispositionService.cs`
- Create: `Services/Returns/ReturnDispositionService.cs`
- Modify: admin return detail UI

- [x] Mặc định hàng tươi là `NotReturned` hoặc `Discarded` tùy quyết định nghiệp vụ.
- [x] Không gọi code tăng `Product.StockQuantity` hoặc `ProductVariant.StockQuantity` khi disposition không phải `Restocked`.
- [x] Chỉ cho chọn `Restocked` đối với nhóm hàng được policy cho phép.
- [x] Bắt buộc ghi QA note và inspector khi chọn `Restocked`.
- [x] Bắt buộc supervisor permission khi restock hàng đã ra khỏi kho.
- [x] Ghi disposition append-only; sửa sai bằng event điều chỉnh, không xóa lịch sử.
- [x] Test mọi disposition của hàng tươi đều không tăng sellable stock.

## Task 1.13 — Seed policy mặc định

- [x] Seed policy cho rau lá, rau thơm, quả mọng và nấm theo thời hạn đã duyệt.
- [x] Seed policy cho trái cây và củ quả thông thường.
- [x] Seed policy cho hàng khô hoặc hàng đóng gói.
- [x] Seed policy từ chối mặc định reason `ChangeOfMind` đối với thực phẩm tươi.
- [x] Seed evidence requirement cho damage, spoilage, temperature và underweight.
- [x] Version seed policy và không sửa trực tiếp policy cũ đã được snapshot.
- [x] Viết admin hoặc command có kiểm soát để tạo version policy mới.

## Task 1.14 — Test và nghiệm thu Phase 1

- [x] Unit test policy precedence product > category > default.
- [x] Unit test boundary chính xác tại deadline và sau deadline một tick.
- [x] Unit test order không thuộc user bị từ chối.
- [x] Unit test claim vượt quantity bị từ chối.
- [x] Unit test hai claim liên tiếp chỉ dùng remaining quantity.
- [x] Unit test state transition hợp lệ và không hợp lệ.
- [x] Unit test partial approval.
- [x] Unit test refund calculator với coupon và combo.
- [x] Unit test shipping fee refund rule.
- [x] Unit test total refund không vượt tiền đã trả.
- [x] Unit test hàng tươi không được hoàn kho.
- [x] Integration test idempotent submit trên SQL Server.
- [x] Integration test hai submit đồng thời không claim vượt quantity.
- [x] Integration test hai admin duyệt đồng thời sinh concurrency conflict.
- [x] Integration test duplicate refund reference bị unique constraint chặn.
- [x] Controller test antiforgery, ownership và permission.
- [x] Playwright test customer submit request và xem timeline.
- [x] Playwright test admin yêu cầu evidence, duyệt một phần và finance xác nhận refund.
- [x] Chạy toàn bộ test suite.
- [x] Chạy `dotnet build Fruitables.csproj --no-restore` và xác nhận 0 errors.
- [x] Review migration và tạo backup database trước khi apply trên môi trường staging.
- [x] Commit Phase 1 theo các commit nhỏ, mỗi commit có test tương ứng.
- [x] **Phase 1 hoàn thành.**

### Kết quả nghiệm thu Phase 1

- Full suite với SQL Server integration bật: **422/422 tests pass, 0 skipped**.
- Build production project: **0 errors, 0 warnings**.
- Migration `AddReturnClaimsFoundation` và `ProtectInternalReturnEvidence` đã apply trên local, rollback thành công trên database test tách biệt.
- Backup SQL Server `Fruitables_PreReturnsPhase1_20260727.bak` đã tạo bằng `COPY_ONLY`, `CHECKSUM` và được xác minh bằng `RESTORE VERIFYONLY`.
- SQL migration đã review tại `docs/returns/add-return-claims-foundation.sql`; backfill legacy bắt buộc dùng danh sách timestamp đã xác minh.

---

# Phase 2 — Production hardening và automation

## Mục tiêu Phase 2

Đưa MVP lên mức vận hành production: xử lý retry, thông báo tin cậy, file an toàn, refund reconciliation, giao bù, fraud controls, analytics và runbook.

## Task 2.1 — Thêm Outbox pattern

**Files dự kiến:**

- Create: `Models/OutboxMessage.cs`
- Create: `Services/Outbox/OutboxDispatcherWorker.cs`
- Create: `Services/Interfaces/IOutboxService.cs`
- Create: `Services/Outbox/OutboxService.cs`
- Modify: `Data/ApplicationDbContext.cs`
- Modify: `Program.cs`

- [ ] Tạo bảng `OutboxMessages` với type, payload, occurred time, processed time, attempt count, next attempt và last error.
- [ ] Tạo index cho message chưa xử lý theo `NextAttemptAtUtc`.
- [ ] Ghi outbox message trong cùng transaction với return state change hoặc refund change.
- [ ] Không gửi email hoặc SignalR trước khi database transaction commit.
- [ ] Worker claim message an toàn khi chạy nhiều instance.
- [ ] Retry theo exponential backoff có giới hạn.
- [ ] Đưa message vượt retry limit vào trạng thái dead-letter.
- [ ] Bảo đảm consumer idempotent.
- [ ] Thêm cleanup policy cho message đã xử lý.
- [ ] Viết integration test transaction rollback không phát notification.

## Task 2.2 — Tự động hóa notification

- [ ] Gửi email khi khách submit yêu cầu.
- [ ] Gửi email khi admin yêu cầu thêm evidence.
- [ ] Gửi email trước khi evidence deadline hết hạn.
- [ ] Gửi email khi request được duyệt một phần, duyệt toàn phần hoặc từ chối.
- [ ] Gửi email khi refund bắt đầu xử lý.
- [ ] Gửi email khi refund thành công hoặc thất bại cần bổ sung thông tin.
- [ ] Gửi SignalR event để refresh admin queue.
- [ ] Gửi SignalR event để refresh timeline của customer.
- [ ] Không đưa internal note hoặc dữ liệu tài chính nhạy cảm vào notification payload.
- [ ] Test retry không gửi cùng email nhiều lần bằng notification idempotency key.

## Task 2.3 — Refund workflow production

- [ ] Tách decision status khỏi refund execution status.
- [ ] Tạo immutable refund attempts hoặc execution logs.
- [ ] Lock hoặc conditional update refund khi worker hoặc finance bắt đầu xử lý.
- [ ] Retry chỉ đối với lỗi có thể retry; lỗi validation phải chuyển manual review.
- [ ] Reconcile refund `Processing` quá timeout.
- [ ] Reconcile tổng refund thành công với `Order.PaymentStatus` projection.
- [ ] Tạo scheduled report cho refund mismatch.
- [ ] Không sử dụng SePay inbound webhook để giả lập giao dịch hoàn tiền.
- [ ] Chỉ tích hợp payout provider khi có tài liệu API, authentication, idempotency và webhook xác nhận chính thức.
- [ ] Nếu tiếp tục hoàn tiền thủ công, tạo queue riêng cho finance và quy trình maker-checker.
- [ ] Mã hóa dữ liệu tài khoản nhận tiền bằng ASP.NET Core Data Protection hoặc encryption service có key rotation.
- [ ] Chỉ hiển thị số tài khoản đã mask ngoài màn hình finance cần thiết.
- [ ] Xóa hoặc anonymize refund destination theo retention policy sau khi hết thời hạn đối soát.
- [ ] Ghi audit khi dữ liệu nhận tiền được xem hoặc thay đổi.

## Task 2.4 — Giao bù bằng replacement order

**Files dự kiến:**

- Modify: `Models/Order.cs`
- Create: `Services/Interfaces/IReplacementOrderService.cs`
- Create: `Services/Returns/ReplacementOrderService.cs`
- Modify: order and return admin/customer views

- [ ] Thêm `OrderType` gồm `Normal` và `Replacement` mà không phá dữ liệu order cũ.
- [ ] Thêm `ParentOrderId` hoặc `OriginalOrderId` cho replacement order.
- [ ] Thêm liên kết từ return request đến replacement order.
- [ ] Chỉ tạo replacement từ approved return item quantity.
- [ ] Snapshot lại shipping address nhưng cho phép customer xác nhận nếu policy yêu cầu.
- [ ] Replacement order có tổng tiền customer phải trả bằng 0 trừ trường hợp business đã duyệt khác.
- [ ] Không đưa replacement order vào gross revenue như đơn bán mới.
- [ ] Dùng cùng cơ chế conditional stock deduction để tránh oversell.
- [ ] Nếu không đủ stock, giữ request ở `ResolutionFailed` hoặc chờ customer chọn refund.
- [ ] Không tạo hai replacement order do double click hoặc retry.
- [ ] Theo dõi shipping status replacement độc lập.
- [ ] Chỉ resolve return request khi replacement hoàn tất theo policy.
- [ ] Test replacement order trừ đúng product hoặc variant stock.

## Task 2.5 — Malware scanning và evidence storage production

- [ ] Tạo abstraction storage để chuyển từ local disk sang private object storage mà không đổi domain service.
- [ ] Chọn private object storage cho staging và production.
- [ ] Dùng signed URL thời hạn ngắn hoặc authorized streaming endpoint.
- [ ] Tích hợp malware scanning trước khi admin có thể tải file gốc.
- [ ] Không render trực tiếp file chưa có `EvidenceScanStatus.Clean`.
- [ ] Reject executable, archive, SVG có script và polyglot file nguy hiểm.
- [ ] Re-encode ảnh nếu policy bảo mật yêu cầu.
- [ ] Xóa EXIF location nếu không cần cho điều tra.
- [ ] Thiết lập lifecycle xóa evidence theo retention policy.
- [ ] Audit mọi lượt tải evidence của admin.
- [ ] Test access URL hết hạn và user khác không đọc được evidence.

## Task 2.6 — Fraud và abuse controls

- [ ] Rate limit endpoint submit return request theo user, IP và order.
- [ ] Rate limit upload evidence.
- [ ] Tính số request, approved amount và rejected rate theo user trong khoảng thời gian cấu hình.
- [ ] Gắn risk flags cho nhiều claim cùng reason, nhiều claim giá trị cao hoặc nhiều địa chỉ liên quan.
- [ ] Gắn risk flag cho ảnh checksum trùng giữa nhiều user hoặc nhiều order.
- [ ] Không auto-reject chỉ dựa trên risk score; chuyển manual review.
- [ ] Bắt buộc supervisor cho override policy hoặc refund vượt ngưỡng.
- [ ] Ghi reason cho mọi override.
- [ ] Hiển thị risk signal cho admin nhưng không lộ rule chi tiết cho customer.
- [ ] Kiểm tra log không chứa full bank account, raw cookie hoặc sensitive evidence URL.

## Task 2.7 — SLA worker

- [ ] Tạo worker tìm request `AwaitingEvidence` đã quá deadline và chuyển `Expired` atomically.
- [ ] Tạo worker cảnh báo request `Submitted` hoặc `UnderReview` sắp quá SLA.
- [ ] Dùng batch size giới hạn và index phù hợp.
- [ ] Dùng `TimeProvider` để test deterministically.
- [ ] Bảo đảm nhiều application instances không xử lý cùng record hai lần.
- [ ] Ghi metric số request quá SLA.
- [ ] Tạo admin filter “Sắp quá hạn” và “Đã quá SLA”.
- [ ] Viết test worker restart không làm mất hoặc xử lý lặp event.

## Task 2.8 — Analytics và báo cáo

**Files dự kiến:**

- Modify: `Services/Analytics/SalesMetricEngine.cs`
- Modify: `Services/SalesAnalyticsService.cs`
- Modify: admin analytics views
- Add return analytics query models/services

- [ ] Tính net revenue từ refund `Succeeded`, không suy luận toàn bộ từ `Order.PaymentStatus`.
- [ ] Không trừ toàn bộ order khi chỉ hoàn một item.
- [ ] Tách gross sales, refund amount, net sales và replacement cost.
- [ ] Báo cáo claim rate theo product, variant và category.
- [ ] Báo cáo claim rate theo reason.
- [ ] Báo cáo approved amount và rejected amount.
- [ ] Báo cáo thời gian xử lý trung bình và tỷ lệ quá SLA.
- [ ] Báo cáo tỷ lệ giao bù so với hoàn tiền.
- [ ] Báo cáo disposition: discarded, quarantined, supplier return và restocked.
- [ ] Chuẩn bị dimension supplier và lot cho Phase 3.
- [ ] Đảm bảo dashboard không double count nhiều refund của cùng order.
- [ ] Viết test analytics cho partial refund và nhiều refund trên một order.

## Task 2.9 — Observability

- [ ] Thêm structured logs với `ReturnRequestId`, `ReturnNumber`, `OrderId`, `RefundId` và correlation id.
- [ ] Không log customer description hoặc evidence metadata nhạy cảm nếu không cần.
- [ ] Thêm metrics:
  - return requests created.
  - return requests approved, partially approved, rejected và expired.
  - refund amount succeeded và failed.
  - refund processing latency.
  - review SLA breached.
  - evidence scan failures.
  - outbox pending và dead-letter count.
- [ ] Thêm health check cho database, object storage và outbox backlog.
- [ ] Thiết lập alert cho refund stuck, outbox dead-letter và SLA breach tăng đột biến.
- [ ] Tạo correlation từ admin action đến return event và refund attempt.

## Task 2.10 — Performance và database hardening

- [ ] Review query plan cho admin queue với dữ liệu lớn.
- [ ] Dùng projection thay vì `Include` toàn bộ graph trên list page.
- [ ] Phân trang server-side cho customer history và admin queue.
- [ ] Không phát sinh N+1 khi hiển thị order items, evidence count hoặc prior refund.
- [ ] Thêm index sau khi đo query thực tế, không thêm index phỏng đoán không dùng.
- [ ] Thêm optimistic concurrency cho policy version nếu admin được chỉnh policy.
- [ ] Thêm command kiểm tra invariant tổng approved quantity và refund amount.
- [ ] Thiết lập database backup và restore drill trước production release.

## Task 2.11 — Production security review

- [ ] Threat model các endpoint customer, admin, file và refund.
- [ ] Kiểm tra IDOR trên return request, evidence, refund và replacement order.
- [ ] Kiểm tra CSRF trên mọi state-changing MVC action.
- [ ] Kiểm tra stored XSS từ customer description, file name và admin notes.
- [ ] Kiểm tra mass assignment trên request models.
- [ ] Kiểm tra authorization cả controller và service boundary cho action tài chính.
- [ ] Thiết lập request body limit phù hợp với upload.
- [ ] Kiểm tra SameSite, Secure và HttpOnly cookie trên production.
- [ ] Kiểm tra retention và quyền xóa dữ liệu cá nhân.
- [ ] Review dependencies và chạy vulnerability scan.
- [ ] Thực hiện manual penetration checklist trước release.

## Task 2.12 — Feature flag và rollout

- [ ] Tạo feature flag `ReturnsModuleEnabled`.
- [ ] Tạo flag riêng cho `ReturnAutoApprovalEnabled` và để tắt mặc định.
- [ ] Tạo flag riêng cho `ReplacementOrdersEnabled` nếu muốn rollout sau refund.
- [ ] Deploy migration trước khi bật UI.
- [ ] Chạy smoke test trên staging với bản sao dữ liệu đã anonymize.
- [ ] Bật nội bộ cho SuperAdmin và nhóm test trước.
- [ ] Bật cho một tỷ lệ nhỏ customer hoặc nhóm order test.
- [ ] Theo dõi errors, refund mismatch, SLA và stock movement.
- [ ] Chuẩn bị rollback bằng cách tắt feature flag; không rollback migration có dữ liệu giao dịch nếu chưa có kế hoạch dữ liệu.
- [ ] Bật toàn bộ sau thời gian quan sát đã chốt.

## Task 2.13 — Runbook vận hành

- [ ] Viết runbook refund bị stuck ở `Processing`.
- [ ] Viết runbook duplicate hoặc mismatched refund reference.
- [ ] Viết runbook evidence scan lỗi.
- [ ] Viết runbook outbox dead-letter.
- [ ] Viết runbook replacement không đủ tồn kho.
- [ ] Viết runbook admin duyệt sai amount.
- [ ] Viết quy trình correction bằng compensating event, không sửa trực tiếp audit rows.
- [ ] Viết quy trình xử lý sự cố lộ evidence URL hoặc refund destination.
- [ ] Ghi rõ owner của CSKH, finance, kho và engineering cho từng loại incident.

## Task 2.14 — Test và nghiệm thu Phase 2

- [ ] Integration test outbox transaction và retry.
- [ ] Integration test refund worker idempotency khi chạy nhiều instance.
- [ ] Integration test reconciliation sửa projection sai nhưng không tạo thêm refund.
- [ ] Integration test replacement order double submit.
- [ ] Integration test evidence authorization và signed URL expiry.
- [ ] Load test admin queue và customer return history ở dung lượng dự kiến.
- [ ] Security test IDOR, CSRF, XSS, upload và mass assignment.
- [ ] Playwright test toàn bộ refund workflow.
- [ ] Playwright test replacement workflow.
- [ ] Kiểm tra accessibility cho form, error message, keyboard và evidence gallery.
- [ ] Kiểm tra responsive ở mobile, tablet và desktop.
- [ ] Chạy toàn bộ test suite và build release.
- [ ] Chạy migration rehearsal trên staging.
- [ ] Hoàn thành backup/restore drill.
- [ ] Product owner, CSKH, finance và kho ký nghiệm thu.
- [ ] **Phase 2 hoàn thành.**

---

# Phase 3 — Kho thực phẩm tươi và truy xuất nguồn gốc

## Mục tiêu Phase 3

Quản lý tồn kho theo lô, hạn sử dụng và FEFO; truy được đơn hàng đã xuất từ lô nào; hàng trả hoặc hàng lỗi đi qua quarantine/waste thay vì tăng tồn bán được.

## Điều kiện bắt đầu Phase 3

- [ ] Phase 2 đã ổn định trên production.
- [ ] Doanh nghiệp xác nhận cần quản lý lot-level inventory.
- [ ] Kho có quy trình ghi nhận batch code, ngày nhập, hạn sử dụng và nhà cung cấp.
- [ ] Xác định sản phẩm quản lý theo đơn vị, kg hoặc đơn vị đo khác.
- [ ] Xác định thời điểm reserve stock: lúc đặt hàng hoặc lúc processing.

## Task 3.1 — Thiết kế mô hình tồn kho lot-based

**Entities dự kiến:**

- `InventoryLot`.
- `InventoryMovement`.
- `InventoryReservation`.
- `OrderItemInventoryAllocation`.
- `SupplierClaim`.

- [ ] `InventoryLot` lưu product, variant, batch code, supplier, received time, expiry time và status.
- [ ] Lưu `OnHandQuantity`, `ReservedQuantity`, `AvailableQuantity` theo invariant đã chốt.
- [ ] Dùng decimal quantity nếu sản phẩm bán theo cân thực tế.
- [ ] `InventoryMovement` là append-only ledger.
- [ ] Hỗ trợ movement types: receive, reserve, release, sale, replacement, quarantine, waste, return-to-supplier, restock và adjustment.
- [ ] `InventoryReservation` liên kết order và lot.
- [ ] `OrderItemInventoryAllocation` ghi chính xác order item đã xuất từ lot nào.
- [ ] `SupplierClaim` liên kết lot và return items có lỗi.
- [ ] Tạo concurrency token hoặc conditional update cho lot quantities.
- [ ] Thiết kế invariant không cho available hoặc reserved quantity âm.

## Task 3.2 — Migration từ aggregate stock

- [ ] Xuất báo cáo `Product.StockQuantity` và `ProductVariant.StockQuantity` hiện tại.
- [ ] Tạm dừng thay đổi tồn kho trong maintenance window hoặc xây dual-write an toàn.
- [ ] Tạo opening balance lot cho tồn kho hiện tại.
- [ ] Reconcile tổng available lot với aggregate stock trước cutover.
- [ ] Không tạo lot giả cho stock âm hoặc dữ liệu không nhất quán; đưa vào exception report.
- [ ] Backup database trước migration.
- [ ] Chạy migration rehearsal trên staging.
- [ ] Chuẩn bị rollback/cutover plan có owner và thời gian cụ thể.

## Task 3.3 — FEFO allocation

- [ ] Chọn lot có expiry gần nhất nhưng vẫn còn sellable trước.
- [ ] Loại lot expired, quarantined, blocked hoặc recalled khỏi allocation.
- [ ] Reserve atomically để tránh oversell.
- [ ] Cho phép một order item lấy từ nhiều lot.
- [ ] Release reservation khi đơn được hủy trước giao.
- [ ] Commit sale movement khi đơn chuyển sang mốc fulfillment đã chốt.
- [ ] Replacement order dùng cùng FEFO allocator.
- [ ] Không tự động release hoặc restock khi order đã delivered.
- [ ] Test concurrent reservation trên cùng lot.

## Task 3.4 — Kết nối return với lot

- [ ] Hiển thị lot đã fulfill cho từng return item.
- [ ] Gắn quality claim với lot và supplier tương ứng.
- [ ] Tự động tăng quality alert count của lot khi có claim được xác nhận.
- [ ] Cho phép quarantine phần tồn còn lại của lot khi vượt ngưỡng cảnh báo.
- [ ] Không đưa hàng khách trả về available stock trước QA.
- [ ] Nếu QA cho phép restock hàng nguyên seal, tạo movement `Restock` có inspector và evidence.
- [ ] Với rau củ tươi, mặc định disposition là waste, quarantine hoặc supplier claim.
- [ ] Tạo recall query tìm tất cả order đã nhận hàng từ một lot.

## Task 3.5 — Expiry và waste management

- [ ] Tạo worker cảnh báo lot sắp hết hạn.
- [ ] Chặn bán lot đã hết hạn.
- [ ] Tự động chuyển lot hết hạn sang trạng thái blocked theo policy.
- [ ] Tạo waste movement khi kho xác nhận tiêu hủy.
- [ ] Báo cáo waste cost theo product, supplier và reason.
- [ ] Báo cáo stock aging.
- [ ] Báo cáo near-expiry inventory để hỗ trợ markdown pricing nếu doanh nghiệp sử dụng.
- [ ] Audit mọi manual inventory adjustment.

## Task 3.6 — UI vận hành kho

- [ ] Tạo màn hình nhận hàng theo lot.
- [ ] Tạo màn hình danh sách lot với filter expiry, supplier và status.
- [ ] Tạo màn hình quarantine/release lot.
- [ ] Tạo màn hình waste và return-to-supplier.
- [ ] Hiển thị inventory movement timeline.
- [ ] Tạo permission `inventory.lots.view`.
- [ ] Tạo permission `inventory.lots.receive`.
- [ ] Tạo permission `inventory.lots.quarantine`.
- [ ] Tạo permission `inventory.lots.adjust`.
- [ ] Yêu cầu supervisor cho manual adjustment vượt ngưỡng.

## Task 3.7 — Chuyển đổi stock projection

- [ ] Quyết định `Product.StockQuantity` và `ProductVariant.StockQuantity` trở thành projection hay bị loại bỏ dần.
- [ ] Nếu giữ projection, cập nhật từ inventory ledger trong cùng transaction hoặc qua projector idempotent.
- [ ] Tạo reconciliation job so sánh projection với tổng available lot.
- [ ] Alert khi có mismatch.
- [ ] Không cho controller hoặc service cũ update trực tiếp stock aggregate sau cutover.
- [ ] Tìm toàn bộ `ExecuteUpdateAsync` đang thay đổi stock và chuyển sang inventory service.
- [ ] Thêm architectural test hoặc code review rule ngăn direct stock mutation mới.

## Task 3.8 — Test và nghiệm thu Phase 3

- [ ] Unit test FEFO chọn đúng lot.
- [ ] Unit test bỏ qua expired và quarantined lot.
- [ ] Integration test concurrent reservation không làm quantity âm.
- [ ] Integration test order cancellation release reservation đúng.
- [ ] Integration test delivered return không tăng available stock.
- [ ] Integration test replacement allocation.
- [ ] Integration test lot quarantine chặn checkout hoặc fulfillment theo policy.
- [ ] Test recall query trả đúng các order bị ảnh hưởng.
- [ ] Test reconciliation phát hiện projection mismatch.
- [ ] Load test movement ledger và lot allocation.
- [ ] Chạy migration rehearsal và inventory reconciliation trên staging.
- [ ] Kho ký nghiệm thu quy trình nhận hàng, quarantine, waste và supplier claim.
- [ ] Finance ký nghiệm thu waste cost và inventory valuation impact.
- [ ] **Phase 3 hoàn thành.**

---

# Release Acceptance — Tiêu chí hoàn thành toàn bộ module

## Nghiệp vụ

- [ ] Khách chỉ claim order và item thuộc quyền sở hữu của mình.
- [ ] Claim chỉ được tạo trong policy window hoặc qua manual override có audit.
- [ ] Hỗ trợ partial quantity và partial refund.
- [ ] Tổng approved quantity không vượt ordered quantity.
- [ ] Tổng refund thành công không vượt tiền khách đã thanh toán.
- [ ] Shipping fee chỉ được hoàn theo policy đã duyệt.
- [ ] Hàng tươi đã giao không tự động quay lại sellable stock.
- [ ] Refund và replacement có idempotency.
- [ ] Policy được snapshot và không thay đổi ngược lịch sử.

## Kỹ thuật

- [ ] Tất cả state transition nằm trong service/state machine tập trung.
- [ ] Tất cả timestamps mới dùng UTC và `TimeProvider`.
- [ ] Transaction boundary đã được integration test trên SQL Server.
- [ ] Concurrency và double-submit đã được kiểm thử.
- [ ] Evidence lưu private và có authorization.
- [ ] Action tài chính có RBAC và audit.
- [ ] Notification dùng outbox.
- [ ] Refund reconciliation hoạt động.
- [ ] Analytics tính partial refund chính xác.
- [ ] Không có direct stock mutation trái inventory policy.

## Chất lượng phát hành

- [ ] `dotnet test Tests/Fruitables.Tests.csproj --no-restore` pass.
- [ ] `dotnet build Fruitables.csproj --configuration Release --no-restore` pass với 0 errors.
- [ ] EF migrations đã được review và rehearsal.
- [ ] Backup và restore drill đã hoàn thành.
- [ ] Security review đã hoàn thành.
- [ ] Performance test đạt SLA đã chốt.
- [ ] Runbook và alert đã sẵn sàng.
- [ ] Feature flags và rollback plan đã được kiểm chứng.
- [ ] Product owner, CSKH, finance, kho và engineering đã ký nghiệm thu.
- [ ] **Module trả hàng được phép mở toàn bộ trên production.**

---

# Checklist cập nhật tài liệu sau mỗi task

- [ ] Đánh dấu task đã hoàn thành bằng `- [x]`.
- [ ] Ghi migration name nếu task có thay đổi database.
- [ ] Ghi link commit hoặc pull request cạnh task tương ứng.
- [ ] Ghi ngày nghiệm thu và người nghiệm thu ở cuối phase.
- [ ] Không đánh dấu phase hoàn thành nếu còn test hoặc tiêu chí acceptance chưa đạt.
