# Refactor luồng trả hàng theo UI

Ngày: 2026-07-28

Trạng thái: đã duyệt trong phiên brainstorming

## Bối cảnh

Module hiện đã xử lý được claim theo từng `OrderItem`, evidence, duyệt một phần, hoàn tiền thủ công, concurrency và audit. Vấn đề chính nằm ở trải nghiệm vận hành:

- Khách hàng thấy các phương án chưa có luồng thực thi như replacement và store credit.
- Màn hình admin trộn việc của CSKH, Finance và kho.
- Finance phải tạo refund và nhập lại số tiền dù hệ thống đã tính số tiền được duyệt.
- UI chưa thu thập tài khoản nhận tiền cho phương thức chuyển khoản thủ công.
- Người dùng phải hiểu quá nhiều trạng thái kỹ thuật.

Bản refactor tập trung vào business flow trên UI. Không viết lại toàn bộ module và không thay đổi các invariant đã được kiểm thử.

## Mục tiêu

1. Khách hàng gửi claim bằng một form ngắn, chỉ gồm item, quantity, reason, mô tả và evidence.
2. CSKH thẩm định hồ sơ và ra quyết định. CSKH không thực hiện chuyển tiền.
3. Một quyết định có số tiền được duyệt tạo đúng một refund task tổng cho cả yêu cầu.
4. Khách hàng cung cấp tài khoản nhận tiền sau khi claim được duyệt.
5. Finance xử lý task với số tiền cố định và xác nhận bằng mã giao dịch cùng chứng từ.
6. UI dùng ngôn ngữ nghiệp vụ và chỉ hiện hành động hợp lệ ở bước hiện tại.

## Phạm vi

### Trong phạm vi

- Form claim của khách hàng.
- Trang theo dõi claim của khách hàng.
- Hàng đợi và màn hình thẩm định của CSKH.
- Form nhập tài khoản nhận tiền.
- Hàng đợi và màn hình xác nhận hoàn tiền của Finance.
- Một migration nhỏ để lưu thông tin nhận tiền đã mã hóa.
- Mapping trạng thái kỹ thuật sang trạng thái hiển thị.
- Kiểm thử luồng ba vai trò.

### Ngoài phạm vi

- Replacement order.
- Store credit.
- Inventory disposition và hoàn kho.
- Payout API hoặc refund về phương thức thanh toán gốc.
- Notification mới.
- SLA worker, fraud scoring và analytics.
- Xóa, đổi thứ tự hoặc đổi numeric value của enum hiện có; sửa migration trả hàng cũ.
- Repository, MediatR, event bus hoặc lớp abstraction mới không phục vụ trực tiếp cho UI flow.

Outbox hiện có được giữ nguyên và không mở rộng trong đợt này.

## Vai trò

### Khách hàng

- Tạo claim cho đơn hàng thuộc sở hữu của mình.
- Bổ sung evidence khi CSKH yêu cầu.
- Theo dõi tiến trình.
- Nhập hoặc sửa thông tin nhận tiền trước khi Finance bắt đầu xử lý.
- Hủy claim khi claim chưa được duyệt.

### CSKH

- Xem hàng đợi claim.
- Yêu cầu khách bổ sung evidence.
- Bắt đầu thẩm định.
- Duyệt toàn phần, duyệt một phần hoặc từ chối.
- Không xem đầy đủ số tài khoản và không xác nhận chuyển tiền.

### Finance

- Xem refund task theo hàng đợi; chỉ xem đầy đủ destination khi khách đã cung cấp.
- Bắt đầu xử lý để khóa thông tin nhận tiền.
- Xác nhận thành công bằng mã giao dịch và chứng từ.
- Đánh dấu thất bại và chọn retry nội bộ hoặc yêu cầu khách sửa tài khoản.
- Không sửa số tiền CSKH đã duyệt.

## Luồng nghiệp vụ

### 1. Khách hàng gửi claim

Từ trang chi tiết đơn đã giao, nút "Báo vấn đề" chỉ xuất hiện khi đơn còn ít nhất một item đủ điều kiện.

Form hiển thị các item còn claimable. Khách hàng chọn item rồi nhập:

- Số lượng bị ảnh hưởng.
- Lý do.
- Mô tả tối thiểu 5 ký tự.
- Evidence nếu policy của reason yêu cầu.

UI không hỏi khách chọn `PartialRefund`, `FullRefund`, `Replacement` hoặc `StoreCredit`. `RequestedResolution` của dữ liệu mới được lưu là `None`. Quyết định hoàn một phần hay toàn bộ do số lượng và số tiền được CSKH duyệt quyết định.

Submit vẫn idempotent. Nếu request đã được tạo nhưng upload file thất bại, hệ thống chuyển khách tới trang chi tiết và hiện CTA tải lại evidence. Hệ thống không tạo request thứ hai.

### 2. CSKH tiếp nhận và thẩm định

Hàng đợi CSKH có bốn tab nghiệp vụ:

- Cần tiếp nhận.
- Chờ khách bổ sung.
- Đang xem xét.
- Đã xử lý.

Trang hồ sơ đặt order snapshot, claim items, prior approved quantity và evidence trong cùng vùng đọc. Khu vực hành động thay đổi theo trạng thái:

- `Submitted`: bắt đầu thẩm định hoặc yêu cầu evidence.
- `AwaitingEvidence`: xem evidence mới và bắt đầu thẩm định khi đủ thông tin.
- `UnderReview`: nhập approved quantity cho từng item, duyệt hoặc từ chối.
- Trạng thái còn lại: chỉ đọc đối với CSKH.

Số tiền được tính lại ở server. UI chỉ hiển thị amount, không gửi amount do người dùng nhập. Duyệt một phần hoặc từ chối bắt buộc có lý do. Phí vận chuyển chỉ được cộng khi lỗi thuộc merchant và toàn bộ đơn bị ảnh hưởng.

### 3. Tạo refund task tổng

Khi quyết định có ít nhất một item được duyệt, hệ thống tạo một `Refund` tổng trong cùng transaction với quyết định:

- `ReturnRequestItemId` để null vì refund đại diện cho cả request.
- `Amount` bằng tổng `ApprovedAmount` của các item cộng phí vận chuyển đã duyệt.
- `Method` là `ManualBankTransfer`.
- `Status` là `AwaitingDestination`.
- `IdempotencyKey` ổn định theo `ReturnRequestId`, không sinh ngẫu nhiên trên mỗi lần render.
- `CreatedByUserId` là CSKH đã duyệt.

Amount được kiểm tra lại theo refund cap của từng item, phí vận chuyển đã duyệt, các refund thành công trước đó và tổng tiền order đã thanh toán. `ReturnRequest.Resolution` là `FullRefund` khi aggregate amount hoàn hết số tiền còn có thể hoàn của order; các trường hợp còn lại là `PartialRefund`.

Luồng mới không tạo refund riêng cho từng item hoặc riêng cho phí vận chuyển. Dữ liệu refund cũ vẫn được đọc như trước.

`ReturnRequest` chuyển sang bước xử lý resolution. Việc tạo refund task thất bại phải rollback cả quyết định để tránh hồ sơ được duyệt nhưng không có task hoàn tiền.

### 4. Khách hàng cung cấp tài khoản nhận tiền

Trang chi tiết claim hiện một action card khi refund ở `AwaitingDestination`:

- Ngân hàng.
- Số tài khoản.
- Tên chủ tài khoản.

Chỉ owner của claim được submit. Sau khi lưu:

- Refund chuyển sang `AwaitingApproval`.
- Khách hàng và CSKH chỉ thấy ngân hàng cùng bốn số cuối.
- Khách hàng có thể sửa thông tin khi refund còn `AwaitingDestination` hoặc `AwaitingApproval`.
- UI khóa form khi Finance chuyển refund sang `Processing`.

### 5. Finance xử lý refund

Hàng đợi Finance có bốn tab:

- Chờ khách cung cấp tài khoản.
- Sẵn sàng chuyển.
- Đang xử lý hoặc cần retry.
- Đã hoàn tất.

Finance mở task ở `AwaitingApproval` và bấm "Bắt đầu xử lý". Thao tác này chuyển refund sang `Processing` bằng conditional update. Nếu người khác đã nhận task, UI báo conflict và tải lại.

Màn hình xử lý hiển thị amount cố định, thông tin nhận tiền, order number và return number. Finance nhập mã giao dịch, tải chứng từ rồi xác nhận. Thành công mới chuyển refund sang `Succeeded`, cập nhật payment projection và kết thúc `ReturnRequest` khi toàn bộ amount đã được hoàn.

Khoản từ 500.000 đồng trở lên yêu cầu người duyệt claim và người xác nhận refund là hai tài khoản khác nhau.

Nếu chuyển khoản thất bại, Finance phải nhập lý do và chọn một trong hai hướng:

- Retry nội bộ: refund chuyển sang `Failed` và request chuyển sang `ResolutionFailed`. Khi Finance retry, hai bản ghi quay lại `Processing` và `ResolutionPending`.
- Tài khoản không hợp lệ: refund về `AwaitingDestination`, request giữ `ResolutionPending`, form của khách được mở lại và không hiển thị chi tiết lỗi nội bộ.

## Trạng thái hiển thị cho khách hàng

UI dùng năm nhóm tiến trình. Database vẫn giữ toàn bộ enum hiện tại.

| Nhóm hiển thị | Trạng thái nguồn | Hành động của khách |
|---|---|---|
| Đã tiếp nhận | `Submitted` | Có thể hủy |
| Cần bổ sung | `AwaitingEvidence` | Tải thêm evidence hoặc hủy |
| Đang xem xét | `UnderReview` | Theo dõi |
| Đang hoàn tiền | `Approved`, `PartiallyApproved`, `ResolutionPending`, `ResolutionFailed` | Nhập tài khoản nếu refund là `AwaitingDestination` |
| Đã kết thúc | `Resolved`, `Rejected`, `Cancelled`, `Expired` | Không có hành động |

Nhóm cuối vẫn hiển thị kết quả cụ thể như "Đã hoàn tiền", "Đã từ chối", "Đã hủy" hoặc "Đã quá hạn". UI không thay các kết quả này bằng một nhãn chung.

## Màn hình

### Chi tiết đơn hàng

- Hiện nút "Báo vấn đề" khi còn item đủ điều kiện.
- Nếu hết hạn hoặc thiếu `DeliveredAtUtc`, hiện lý do và hướng khách liên hệ CSKH.

### Form claim

- Một trang, không dùng wizard.
- Field của item chưa chọn bị disable và không tham gia validation.
- Reason chỉ chứa lựa chọn được policy hỗ trợ.
- Badge và input evidence đổi theo reason của item đã chọn.
- Nút submit bị khóa sau lần submit hợp lệ đầu tiên.

### Chi tiết claim của khách

- Status và action cần làm nằm đầu trang.
- Hiện item, quantity, reason, amount đã duyệt và timeline.
- Hiện form evidence hoặc tài khoản chỉ khi cần.
- Không hiện internal note, full bank account hoặc state kỹ thuật.

### Hồ sơ CSKH

- Chỉ có action phù hợp với trạng thái hiện tại.
- Không còn form disposition.
- Không còn dropdown resolution.
- Nút duyệt cho biết tổng amount sẽ chuyển sang Finance.

### Task Finance

- Không có input amount.
- Full bank account chỉ xuất hiện trên màn hình có permission Finance.
- Có action bắt đầu, xác nhận thành công, đánh dấu thất bại và yêu cầu khách sửa tài khoản.
- Mã giao dịch và chứng từ là bắt buộc khi xác nhận thành công.

## Dữ liệu nhận tiền và migration

Migration thêm các field tối thiểu vào `Refund`:

- `DestinationBankCode`.
- `DestinationAccountNumberProtected`.
- `DestinationAccountLast4`.
- `DestinationAccountHolderProtected`.
- `DestinationSubmittedAtUtc`.

Số tài khoản và tên chủ tài khoản được bảo vệ bằng ASP.NET Core Data Protection. Production phải dùng key ring bền vững và giới hạn quyền đọc key. Không ghi plaintext vào database, log, TempData, outbox payload hoặc email.

Mỗi lần khách cập nhật destination phải ghi audit event. Mỗi lần Finance mở full destination cũng phải ghi audit event. Spec cho phép thêm các audit type mới vào cuối `ReturnEventType`; không đổi numeric value cũ.

Khi refund thành công, hệ thống xóa hai ciphertext chứa số tài khoản và tên chủ tài khoản trong cùng transaction. Hệ thống giữ bank code, bốn số cuối, transaction reference, chứng từ và audit event để đối soát.

## Validation và lỗi

- Ownership, eligibility, quantity, evidence requirement và refund cap được kiểm tra ở server.
- POST tiếp tục dùng antiforgery.
- Invalid hoặc stale `RowVersion` trả thông báo conflict, không ghi một phần quyết định.
- Duplicate submit và duplicate refund task trả bản ghi hiện có.
- Duplicate transaction reference bị chặn.
- Upload lỗi không xóa request đã submit; UI cho phép retry.
- Customer không được sửa destination khi refund đã `Processing`.
- CSKH không được giải mã destination.
- Finance không được xác nhận refund nếu thiếu permission, mã giao dịch hoặc chứng từ.
- Nội dung lỗi dành cho khách không để lộ internal note, bank account hoặc chi tiết vận hành Finance.

## Tiêu chí nghiệm thu

1. Customer chỉ claim order và item thuộc quyền sở hữu, còn thời hạn và còn quantity.
2. Form không còn replacement, store credit, disposition hoặc amount nhập tay.
3. Evidence bắt buộc đúng theo reason.
4. CSKH duyệt partial quantity và server tính lại amount.
5. Mỗi request được duyệt tạo đúng một aggregate refund task, kể cả khi retry.
6. Decision và refund task được commit hoặc rollback cùng nhau.
7. Chỉ owner nhập destination; dữ liệu lưu trong database không chứa số tài khoản plaintext.
8. Customer và CSKH chỉ thấy last4. Finance có permission mới xem đầy đủ.
9. Finance không sửa amount và phải cung cấp reference cùng proof.
10. Maker-checker, duplicate reference và optimistic concurrency vẫn được giữ.
11. Thành công xóa full destination nhưng giữ dữ liệu audit tối thiểu.
12. Customer thấy đúng status, CTA và thông báo lỗi theo từng bước.
13. Return và outbox test hiện có vẫn pass.
14. Có Playwright flow cho Customer, CSKH và Finance.
15. Release build pass.

## Điều kiện rollout

Migration không chuyển đổi refund lịch sử. Trước khi bật UI mới, deployment phải xuất báo cáo các request chưa terminal:

- Request đã có refund theo từng item phải được xử lý xong bằng luồng cũ trước khi bật UI mới.
- Request đã duyệt nhưng chưa có refund có thể tạo aggregate task bằng command idempotent được review riêng.
- Refund terminal chỉ được hiển thị read-only và không bị sửa dữ liệu.

Không bật UI mới khi báo cáo còn request không khớp hai trường hợp trên.

## Thứ tự sau refactor

1. Hoàn thành UI flow, aggregate refund task và destination encryption trong spec này.
2. Thêm malware scanning hoặc quarantine trước khi mở evidence upload công khai trên production.
3. Thêm email hoặc SignalR qua outbox sau khi luồng vận hành đã ổn định.
4. Thay chuyển khoản thủ công bằng payout provider khi có API, idempotency contract và webhook xác nhận chính thức.

Payout API sẽ thay bước nhập destination và xử lý chuyển khoản thủ công. Các bước claim, review, approved amount và audit không đổi.
