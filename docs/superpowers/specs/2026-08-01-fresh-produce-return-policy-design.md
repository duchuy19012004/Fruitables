# Đặc tả xử lý yêu cầu hoàn tiền rau củ quả tươi

- Ngày: 2026-08-01
- Trạng thái: Đã duyệt
- Tài liệu nghiệp vụ nguồn: `Business-Logic/business-logic-tra-hang-rau-cu-qua-cap-nhat.md`
- Phạm vi hệ thống: module Returns hiện tại của Fruitables

## 1. Mục tiêu

Chuẩn hóa quy trình tiếp nhận, thẩm định và hoàn tiền cho rau củ quả tươi theo các nguyên tắc sau:

- Bảo vệ quyền lợi khách khi hàng hư, giao thiếu hoặc giao sai.
- Không tự động chấp nhận yêu cầu chỉ vì khách đã gửi ảnh hoặc video.
- Cho phép xử lý nhanh các yêu cầu có giá trị thấp và ít rủi ro.
- Ngăn hoàn trùng, xử lý trùng số kg và cộng nhầm hàng hư vào tồn kho.
- Tách riêng quyết định dành cho khách, cách xử lý hàng và bên chịu chi phí.
- Lưu đủ lịch sử để nhân viên giải thích được vì sao hệ thống hoặc người duyệt đưa ra quyết định.

## 2. Phạm vi

### 2.1. Có trong giai đoạn này

- Đơn hàng đã giao thành công.
- Rau củ quả bán theo kg nguyên, tối thiểu `1 kg`.
- Hoàn tiền toàn bộ hoặc một phần.
- Yêu cầu có một hoặc nhiều sản phẩm.
- Quyết định riêng cho từng sản phẩm.
- Tự duyệt theo quy tắc đối với yêu cầu giá trị thấp và ít rủi ro.
- Nhân viên xem xét và quản lý phê duyệt theo ngưỡng tiền.
- Kháng nghị một lần đối với sản phẩm bị từ chối.
- Khách hủy yêu cầu trước khi có đề xuất quyết định, giữ nguyên hành vi hiện có của module Returns.
- Hoàn về phương thức thanh toán gốc khi có thể, chuyển khoản ngân hàng khi không thể.
- Ghi nhận nguyên nhân, bên chịu chi phí và cách xử lý hàng riêng biệt.
- Dashboard quản lý chính sách và thẩm quyền phê duyệt.

### 2.2. Chưa có trong giai đoạn này

- Giao bù sản phẩm.
- Voucher hoặc store credit.
- Thu hồi hàng vật lý.
- Nhập lại kho hàng đã rời khỏi quyền kiểm soát của cửa hàng.
- Dùng AI để kết luận nội dung ảnh hoặc video có hợp lệ hay không.
- Tự động đối soát hoặc thu hồi chi phí từ đơn vị vận chuyển.
- Tự động loại trừ ngày nghỉ lễ khỏi SLA. Ngày làm việc hiện được tính từ thứ Hai đến thứ Sáu.

## 3. Ngôn ngữ hiển thị

Không dùng từ `bằng chứng` trong nội dung dành cho khách vì từ này mang tính pháp lý và dễ tạo cảm giác khách đang bị nghi ngờ.

- Nhãn tải tệp: `Ảnh/video về tình trạng sản phẩm`.
- Khi cần thêm thông tin: `Vui lòng gửi thêm ảnh/video để cửa hàng kiểm tra`.
- Trên màn hình nhân viên: `Ảnh/video và thông tin khách cung cấp`.
- Trong tài liệu nghiệp vụ: `thông tin xác minh`.
- Khi phát hiện tệp trùng: `Ảnh/video đã xuất hiện trong một yêu cầu khác`.

Tên kỹ thuật nội bộ như `ReturnEvidence` có thể giữ nguyên vì khách không nhìn thấy tên này.

## 4. Nguyên tắc nghiệp vụ

1. Chỉ đơn đã giao thành công mới được gửi yêu cầu.
2. Thời hạn được xác định theo chính sách đang có hiệu lực tại thời điểm khách gửi.
3. Chính sách được chọn theo thứ tự sản phẩm, danh mục, mặc định; trong mỗi phạm vi còn phải khớp lý do.
4. Một yêu cầu có thể chứa nhiều sản phẩm, nhưng mỗi sản phẩm có số kg, mức hư hỏng, thông tin xác minh và quyết định riêng.
5. Số kg yêu cầu là số nguyên, tối thiểu `1 kg` và không vượt số kg đã mua hoặc số kg còn được phép khiếu nại.
6. Mức hư hỏng thuộc tập cố định `25%`, `50%`, `75%`, `100%`; chính sách có thể bật một tập con của bốn mức này.
7. Giao thiếu và giao sai luôn dùng mức `100%` cho số kg được chấp nhận.
8. Nhân viên có thể duyệt số kg hoặc tỷ lệ thấp hơn mức khách yêu cầu, nhưng phải nêu lý do cụ thể.
9. Một phần hàng chỉ được xử lý bồi thường một lần.
10. Tiền hoàn dựa trên số tiền khách thực trả sau mọi giảm giá.
11. Hàng hư được duyệt không quay lại kho và không làm tăng tồn bán được.
12. Cửa hàng giải quyết quyền lợi khách trước; việc phân bổ chi phí với đơn vị vận chuyển diễn ra sau và không làm chậm khoản hoàn.

## 5. Kiến trúc nghiệp vụ

Giải pháp mở rộng module Returns hiện tại thay vì xây một workflow engine mới.

### 5.1. Các thành phần

`PolicyResolver` chọn chính sách khớp chính xác lý do theo thứ tự sản phẩm, danh mục trực tiếp của sản phẩm, mặc định. Hệ thống không cho phép hai phiên bản cùng phạm vi, đối tượng và lý do có thời gian hiệu lực chồng nhau. Nếu dữ liệu cũ vẫn bị chồng thời gian, phiên bản cao hơn được ưu tiên, sau đó đến thời điểm bắt đầu hiệu lực mới hơn. Kết quả được chụp lại trên từng sản phẩm khi khách gửi yêu cầu.

`EligibilityChecker` kiểm tra quyền sở hữu đơn, trạng thái giao hàng, hạn gửi yêu cầu, số kg còn lại và các lần xử lý trước.

`EvidenceValidator` là tên kỹ thuật nội bộ. Thành phần này kiểm tra tệp được gắn đúng sản phẩm, định dạng, trạng thái quét an toàn và checksum. Thành phần này không kết luận nội dung ảnh hoặc video.

`RiskEvaluator` đánh giá các điều kiện tự duyệt, điểm bất thường và mức hỗ trợ áp dụng cho tài khoản.

`DecisionService` lưu đề xuất của nhân viên, quyết định từng sản phẩm và yêu cầu quản lý phê duyệt khi tổng tiền vượt thẩm quyền.

`RefundService` tạo và thực thi một khoản hoàn tổng hợp sau khi mọi sản phẩm đã có kết quả cuối cùng.

`DispositionService` ghi nhận phần hàng hư đã được khách xử lý, hàng giao sai được khách giữ hoặc trường hợp không có hàng vật lý.

`ReturnEvent` lưu lịch sử thay đổi. Sự kiện liên quan đến một sản phẩm phải có tham chiếu đến sản phẩm đó để giữ được lịch sử quyết định riêng.

### 5.2. Ranh giới trách nhiệm

- Bộ chọn chính sách không đưa ra quyết định hoàn tiền.
- Bộ kiểm tra điều kiện không đọc nội dung ảnh hoặc video.
- Bộ đánh giá rủi ro chỉ trả về kết quả và mã quy tắc; bộ điều phối quyết định mới được đổi trạng thái.
- Bộ hoàn tiền không được tự thay đổi số kg hoặc tỷ lệ đã duyệt.
- Bộ xử lý hàng không được quyết định quyền lợi của khách.

## 6. Cấu hình và dashboard

Ngưỡng tiền, tỷ lệ đơn hàng và thời hạn được lưu trong database và quản lý qua dashboard. Bốn mức hư hỏng `25/50/75/100%` là tập giá trị cố định của nghiệp vụ; chính sách chỉ quyết định mức nào trong tập này được phép dùng.

### 6.1. Chính sách trả hàng

Dashboard cho phép tạo phiên bản chính sách theo phạm vi mặc định, danh mục hoặc sản phẩm và theo từng lý do.

Mỗi phiên bản có:

- Tên chính sách.
- Phạm vi và đối tượng áp dụng.
- Lý do áp dụng.
- Ngày bắt đầu và ngày kết thúc hiệu lực.
- Thời hạn gửi yêu cầu.
- Có bắt buộc ảnh/video hay không.
- Các mức hư hỏng được bật trong tập `25%`, `50%`, `75%`, `100%`; mặc định bật cả bốn.
- Với rau củ quả tươi, bắt buộc cho phép cả hoàn đúng mức khách yêu cầu và hoàn thấp hơn yêu cầu.
- Bật hoặc tắt tự duyệt.
- Trần tiền tự duyệt, mặc định `100.000₫`.
- Tỷ lệ tối đa so với giá trị hàng hóa thực trả của đơn, mặc định `30%`.
- Tỷ lệ hậu kiểm yêu cầu tự duyệt, mặc định `10%`.
- Thời hạn khách bổ sung thông tin, mặc định `24 giờ`.
- Thời hạn kháng nghị, mặc định `24 giờ`.

Mỗi lần chỉnh sửa tạo phiên bản mới. Yêu cầu đã gửi giữ snapshot cũ và không bị thay đổi hồi tố.

Chính sách cho lý do giao thiếu hoặc giao sai bắt buộc bật mức `100%`; dashboard và database đều không cho lưu phiên bản vi phạm quy tắc này.

`AllowFullRefund` nghĩa là được duyệt đúng toàn bộ số tiền khách yêu cầu cho sản phẩm. `AllowPartialRefund` nghĩa là được duyệt số tiền thấp hơn do giảm số kg hoặc giảm mức hư hỏng. Chính sách hợp lệ cho nhóm rau củ quả tươi phải bật cả hai; các cờ này chỉ có thể khác đối với nhóm hàng ngoài phạm vi tài liệu.

Dashboard phải cho nhân viên xem trước chính sách sẽ được chọn với một tổ hợp sản phẩm, danh mục, lý do và thời điểm cụ thể.

### 6.2. Thẩm quyền phê duyệt

Dashboard thẩm quyền lưu ngưỡng theo vai trò:

- Nhân viên có thẩm quyền xử lý thủ công khoản dưới `500.000₫`.
- Khoản từ `500.000₫` phải có quản lý phê duyệt.
- Người đề xuất và người phê duyệt phải khác nhau.
- Ngưỡng tách người tạo khoản hoàn và người thực thi giao dịch tài chính mặc định là `500.000₫`.

Ngưỡng tự duyệt chỉ thuộc chính sách trả hàng và không được khai báo lặp lại trong ma trận thẩm quyền. Ngưỡng phê duyệt theo vai trò và ngưỡng tách nhiệm vụ tài chính là dữ liệu khởi tạo, có thể được thay đổi bởi tài khoản có quyền quản lý chính sách. Thay đổi phải có ngày hiệu lực và audit.

Ngưỡng thẩm quyền được lấy tại thời điểm version gói quyết định được gửi phê duyệt và lưu trên bản ghi phê duyệt. Ngưỡng tách nhiệm vụ tài chính được lấy và snapshot khi tạo khoản hoàn. Thay đổi cấu hình sau đó không làm đổi một nhiệm vụ hoặc khoản hoàn đang xử lý.

### 6.3. SLA

Thời hạn gửi yêu cầu, bổ sung thông tin và kháng nghị thuộc chính sách từng sản phẩm và được snapshot lúc gửi. SLA xem xét là cấu hình vận hành được snapshot lúc gửi yêu cầu. SLA hoàn tiền được snapshot khi tạo khoản hoàn. Giá trị khởi tạo là:

- Quyết định trong `24 giờ` từ lúc nhận một yêu cầu ban đầu đã đủ thông tin.
- Hành động đầu tiên trong `24 giờ` nếu yêu cầu ban đầu chưa đủ thông tin; hành động này phải nêu rõ nội dung cần bổ sung.
- Khách có `24 giờ` để bổ sung ảnh/video khi cửa hàng yêu cầu.
- Cửa hàng có `24 giờ` để ra quyết định sau khi khách bổ sung đủ thông tin.
- Cửa hàng có `24 giờ` để ra quyết định sau khi nhận một kháng nghị hợp lệ.
- Khoản hoàn được hoàn tất trong `3 ngày làm việc` sau khi quyết định có hiệu lực và cửa hàng đã có đủ thông tin nhận tiền.
- Ngày làm việc hiện là thứ Hai đến thứ Sáu, không tự động loại trừ ngày nghỉ lễ.

SLA quyết định được tính ở cấp yêu cầu để phù hợp với một gói phê duyệt và một khoản hoàn tổng hợp. Nếu mọi sản phẩm đã đủ thông tin lúc gửi, đồng hồ `24 giờ` bắt đầu ngay. Nếu bất kỳ sản phẩm nào cần khách bổ sung, đồng hồ quyết định của yêu cầu tạm dừng; khi mọi sản phẩm đã bổ sung hợp lệ hoặc hết hạn, một chu kỳ `24 giờ` mới bắt đầu. Kháng nghị hợp lệ mở một chu kỳ `24 giờ` mới cho toàn yêu cầu. Phê duyệt của quản lý nằm trong cùng SLA, nên đề xuất của nhân viên chưa được tính là quyết định hoàn tất.

## 7. Dữ liệu cần lưu

### 7.1. Cấp yêu cầu

Yêu cầu cha giữ:

- Đơn hàng và khách hàng.
- Trạng thái tổng hợp.
- Tổng tiền đề nghị và tổng tiền được duyệt.
- Mức rủi ro cao nhất của các sản phẩm và điều kiện cấp yêu cầu.
- Có hoàn phí vận chuyển hay không.
- Người xem xét, người phê duyệt và các mốc SLA.
- Phiên bản dữ liệu để xử lý cập nhật đồng thời.

### 7.2. Cấp sản phẩm

Mỗi `ReturnRequestItem` giữ:

- Số kg khách yêu cầu.
- Tỷ lệ khách chọn.
- Số kg được duyệt.
- Tỷ lệ được duyệt.
- Số kg, tỷ lệ, lý do và người đề xuất trong version gói quyết định hiện tại.
- Số tiền đề nghị và số tiền được duyệt.
- Trạng thái quyết định riêng.
- Mức rủi ro hiện tại.
- Mã các quy tắc đã kích hoạt trong sự kiện đánh giá.
- Nguyên nhân.
- Bên chịu chi phí.
- Lý do quyết định.
- Số lần kháng nghị và hạn kháng nghị.
- Snapshot chính sách, đơn vị `kg`, giá trị thực trả và hạn gửi yêu cầu.

`RequestedQuantity` và `ApprovedQuantity` tiếp tục là số nguyên, được hiểu là kg đối với nhóm sản phẩm này. Hai trường tỷ lệ mới biểu diễn phần hư trong số kg đã chọn.

`RequestedQuantity` phải từ `1` trở lên. `ApprovedQuantity` có thể bằng `0` chỉ khi từ chối sản phẩm; nếu quyết định chấp nhận thì số kg được duyệt phải từ `1` trở lên.

### 7.3. Ảnh/video và thông tin xác minh

- Tệp khách tải lên phải gắn với ít nhất một sản phẩm trong yêu cầu.
- Cùng một ảnh kiện hàng có thể gắn với nhiều sản phẩm trong cùng yêu cầu.
- Quan hệ giữa tệp và sản phẩm là nhiều-nhiều: hệ thống chỉ lưu một tệp và tạo một liên kết cho mỗi sản phẩm liên quan.
- Checksum chỉ bị coi là trùng đáng chú ý khi tệp đã xuất hiện ở một yêu cầu khác.
- Tệp nội bộ và ảnh giao dịch hoàn tiền có thể ở cấp yêu cầu nhưng không hiển thị cho khách.

### 7.4. Phê duyệt

Mỗi yêu cầu có một nhân viên phụ trách tại một thời điểm. Nhân viên này chịu trách nhiệm cho mọi đề xuất thủ công trong gói quyết định. Việc chuyển người phụ trách phải được ghi thành sự kiện và người từng đề xuất trong phiên bản gói hiện tại không được phê duyệt chính phiên bản đó.

Một bản ghi phê duyệt cấp yêu cầu giữ version gói quyết định, tổng tiền đề xuất, danh sách người đã đề xuất, người phê duyệt, kết quả, lý do và thời điểm. Quản lý chấp nhận hoặc trả lại toàn bộ version gói; không phê duyệt riêng lẻ từng dòng trong cùng version. Việc quản lý từ chối gói đề xuất có nghĩa trả yêu cầu về `UnderReview`, không tự động từ chối quyền lợi của khách. Bản ghi không được ghi đè khi quyết định thay đổi; thay đổi tạo version và bản ghi mới.

### 7.5. Xử lý hàng

Phần hàng được xử lý cần lưu khối lượng tương đương dạng thập phân. Ví dụ `1 kg × 50%` tạo `0,5 kg` hàng hư được khách xử lý.

Các kết quả cần phân biệt:

- `DisposedByCustomer`: phần hàng hư được khách tự xử lý.
- `CustomerKeptWrongItem`: khách giữ hàng giao nhầm.
- `NoPhysicalItem`: giao thiếu, không có hàng để xử lý.

Không có kết quả hoàn kho trong phạm vi này.

### 7.6. Mức hỗ trợ tài khoản

Mức hạn chế hỗ trợ không nên lưu như một cờ không có lịch sử. Mỗi thay đổi cần có mức, lý do, người tạo, người phê duyệt nếu cần, ngày hiệu lực và ngày kết thúc nếu có.

## 8. Trạng thái

### 8.1. Trạng thái cấp sản phẩm

Luồng chính:

`Submitted -> AwaitingCustomerInfo hoặc UnderReview`

`UnderReview -> DecisionProposed`

`DecisionProposed -> Approved, RejectedPendingAppeal hoặc AwaitingManagerApproval`

`AwaitingManagerApproval -> Approved, RejectedPendingAppeal hoặc UnderReview`

Luồng tự duyệt:

`Submitted -> Approved`

Luồng bổ sung thông tin:

`AwaitingCustomerInfo -> UnderReview`

Nếu khách không bổ sung đúng hạn:

`AwaitingCustomerInfo -> Expired`

Quản lý mở lại một lần khi có lý do:

`Expired -> AwaitingCustomerInfo hoặc UnderReview`

Khách hủy trước khi có quyết định:

`Submitted, AwaitingCustomerInfo hoặc UnderReview -> Cancelled`

Việc hủy từ `UnderReview` chỉ hợp lệ khi chưa có sản phẩm nào có đề xuất quyết định, quyết định cuối cùng hoặc nhiệm vụ phê duyệt.

Luồng kháng nghị:

`RejectedPendingAppeal -> UnderReview hoặc Rejected`

Sản phẩm quay lại `UnderReview` khi khách kháng nghị hợp lệ. Nếu hết hạn mà khách không kháng nghị, sản phẩm chuyển sang `Rejected` và trở thành quyết định cuối cùng.

`DecisionProposed` là trạng thái nội bộ và không hiển thị như một quyết định cho khách. `Approved` là trạng thái quyết định cuối cùng cho cả duyệt đúng yêu cầu và duyệt thấp hơn yêu cầu. Trường hợp duyệt thấp hơn được ghi bằng kết quả sự kiện `PartiallyApproved`, không tạo thêm một trạng thái sản phẩm.

### 8.2. Trạng thái cấp yêu cầu

Trạng thái cha được tổng hợp từ các sản phẩm:

- Có sản phẩm đang chờ khách thì yêu cầu ở `AwaitingEvidence` về mặt kỹ thuật, nhưng giao diện hiển thị `Cần thêm ảnh/video`.
- Có sản phẩm đang xem xét hoặc chờ quản lý thì yêu cầu ở `UnderReview`.
- Còn sản phẩm ở `RejectedPendingAppeal` thì yêu cầu vẫn ở `UnderReview`; hệ thống chưa tạo khoản hoàn.
- Tất cả đã có kết quả cuối cùng và không sản phẩm nào được duyệt thì yêu cầu ở `Rejected`.
- Tất cả đã có kết quả cuối cùng và có ít nhất một sản phẩm được duyệt thì yêu cầu ở `ResolutionPending`.
- Khoản hoàn đang chờ tài khoản ngân hàng ở `AwaitingDestination`; yêu cầu cha vẫn ở `ResolutionPending` và giao diện hiển thị `Cần thông tin nhận tiền`.
- Hoàn tiền thất bại thì yêu cầu ở `ResolutionFailed`.
- Hoàn tiền thành công thì yêu cầu ở `Resolved`.
- Tất cả sản phẩm hết hạn thì yêu cầu ở `Expired`.
- Khách hủy hợp lệ trước khi có quyết định thì yêu cầu ở `Cancelled` và giải phóng số kg đang giữ chỗ.

Trạng thái `Approved` và `PartiallyApproved` vẫn được ghi trong sự kiện quyết định để audit, dù yêu cầu chuyển tiếp sang `ResolutionPending` khi tạo khoản hoàn.

## 9. Luồng gửi yêu cầu

1. Khách chọn đơn đã giao.
2. Khách chọn một hoặc nhiều sản phẩm.
3. Với mỗi sản phẩm, khách chọn số kg bị ảnh hưởng, lý do, mức `25/50/75/100%`, nhập mô tả và tải ảnh/video nếu chính sách yêu cầu. Với giao thiếu hoặc giao sai, giao diện tự đặt mức `100%` và không cho đổi tỷ lệ.
4. Hệ thống kiểm tra mỗi sản phẩm chỉ xuất hiện một lần trong yêu cầu.
5. Hệ thống phân giải và snapshot chính sách cho từng sản phẩm.
6. Hệ thống kiểm tra hạn gửi, số kg còn lại và các yêu cầu đang giữ chỗ.
7. Hệ thống tính khoản tiền đề nghị.
8. Hệ thống lưu yêu cầu, giữ chỗ số kg và ghi sự kiện trong cùng giao dịch.
9. Hệ thống quét tệp và đánh giá điều kiện tự duyệt.
10. Các sản phẩm không tự duyệt được đưa vào hàng đợi của nhân viên.

Thời điểm đúng bằng hạn cuối vẫn hợp lệ. Sau hạn cuối dù chỉ một đơn vị thời gian thì không hợp lệ.

## 10. Công thức hoàn tiền

### 10.1. Giá trị thực trả

Giảm giá cấp đơn được phân bổ cho từng dòng theo tỷ trọng giá trị dòng. Phần lẻ được phân bổ ổn định theo thứ tự dòng để tổng phân bổ đúng bằng tổng giảm giá.

Giá trị thực trả của dòng bao gồm:

- Giá sau khuyến mãi sản phẩm.
- Giảm giá combo đã áp dụng.
- Phần giảm giá cấp đơn được phân bổ.

### 10.2. Công thức từng sản phẩm

`Tiền hoàn = (giá trị thực trả của dòng / số kg đã mua) × số kg được duyệt × tỷ lệ được duyệt`

Nhân viên chỉ được chọn một tỷ lệ trong tập `25/50/75/100%` đã bật và không cao hơn mức khách yêu cầu. Tỷ lệ `0%` không được lưu; từ chối được biểu diễn bằng `ApprovedQuantity = 0`. Với giao thiếu hoặc giao sai, nhân viên chỉ được giảm số kg hoặc từ chối, không được giảm tỷ lệ dưới `100%`.

Kết quả được làm tròn đến đồng gần nhất; phần đúng `0,5₫` được làm tròn ra xa số `0`.

Ví dụ:

- Khách mua `3 kg`.
- Giá trị thực trả của dòng là `300.000₫`.
- Cửa hàng duyệt `1 kg` ở mức `50%`.
- Tiền hoàn là `50.000₫`.

### 10.3. Giới hạn

- Trừ các khoản hoàn thành công trước đó cho cùng phần hàng.
- Không trừ khoản hoàn thất bại hoặc đã hủy.
- Tổng hoàn thành công của đơn không vượt số tiền khách đã thanh toán.
- Một số kg đã có quyết định cuối cùng không được đưa vào yêu cầu mới.
- Yêu cầu bị khách hủy trước khi có quyết định sẽ giải phóng số kg đang giữ chỗ.

## 11. Tự duyệt

### 11.1. Điều kiện cấp yêu cầu

Tự duyệt dùng phép `AND`. Tổng khoản bồi thường dự kiến, gồm tiền hàng và phí vận chuyển nếu có, phải:

- Không vượt trần tiền, mặc định `100.000₫`.
- Không vượt tỷ lệ cấu hình, mặc định `30%` giá trị hàng hóa thực trả của đơn, không gồm phí vận chuyển.

Ngưỡng tự duyệt có nguồn duy nhất là snapshot `ReturnPolicy` của từng sản phẩm. Khi nhiều sản phẩm có ngưỡng khác nhau, yêu cầu dùng trần tiền thấp nhất và tỷ lệ thấp nhất trong các chính sách cho phép tự duyệt. Sản phẩm có chính sách tắt tự duyệt luôn được chuyển sang xem xét thủ công.

Khi kiểm tra ngưỡng, hệ thống cộng yêu cầu hiện tại với mọi khoản đã duyệt, đã hoàn hoặc đang giữ chỗ trước đó của cùng đơn hàng, không phụ thuộc thời hạn của chính sách đã áp dụng cho yêu cầu trước. Cách tính này ngăn việc chia nhỏ thành nhiều sản phẩm hoặc nhiều yêu cầu để lách ngưỡng. Mẫu số của tỷ lệ vẫn là giá trị hàng hóa thực trả của đơn, không gồm phí vận chuyển.

### 11.2. Điều kiện cấp sản phẩm

Sản phẩm chỉ được tự duyệt khi:

- Đủ điều kiện theo chính sách.
- Có ít nhất một ảnh/video hợp lệ gắn với sản phẩm, kể cả khi chính sách cho phép gửi yêu cầu thủ công mà không có ảnh/video.
- Tệp đã quét an toàn.
- Tệp chưa xuất hiện ở yêu cầu khác.
- Số kg chưa được xử lý hoặc giữ chỗ ở yêu cầu khác.
- Không có khoản bồi thường trùng.
- Tài khoản không ở mức tăng xác minh hoặc cao hơn.

### 11.3. Điều kiện chặn toàn yêu cầu

Một điều kiện cấp yêu cầu sẽ chặn tự duyệt cho tất cả sản phẩm, gồm:

- Tổng tiền hoặc tỷ lệ vượt ngưỡng.
- Tài khoản đang bị tăng xác minh.
- Có checksum xuất hiện ở yêu cầu khác.
- Cấu hình tự duyệt thiếu hoặc không hợp lệ.

Điều kiện chỉ liên quan một sản phẩm, chẳng hạn thiếu ảnh của sản phẩm đó, chỉ chuyển sản phẩm đó sang xem xét thủ công nếu không có điều kiện chặn cấp yêu cầu.

Nhận xét rằng nội dung khách gửi mâu thuẫn với dữ liệu đóng gói hoặc giao nhận chỉ được ghi sau khi nhân viên kiểm tra thủ công. Giai đoạn này không dùng một quy tắc tự động mơ hồ để suy ra mâu thuẫn đó.

### 11.4. Giới hạn kiểm tra tự động

Hệ thống chỉ kiểm tra dữ liệu và thuộc tính kỹ thuật của tệp. Hệ thống không dùng AI để kết luận ảnh có đúng sản phẩm hoặc có thể hiện đúng mức hư hỏng hay không.

Dashboard cho phép cấu hình tỷ lệ hậu kiểm, mặc định `10%`. Kết quả hậu kiểm ảnh hưởng mức hỗ trợ của các yêu cầu sau. Khoản hoàn đã hoàn tất không bị tự động thu hồi. Giai đoạn này không thêm luồng tạm dừng hoặc đảo ngược một quyết định tự duyệt.

## 12. Xem xét thủ công và phê duyệt

Nhân viên kiểm tra:

- Ảnh/video và mô tả của khách.
- Loại sản phẩm và số kg đã mua.
- Thời điểm giao và thời điểm gửi yêu cầu.
- Thông tin đóng gói, khối lượng kiện hàng và ảnh trước giao nếu có.
- Sự cố giao nhận.
- Phản ánh liên quan cùng lô hàng, sản phẩm hoặc tuyến giao.
- Lịch sử yêu cầu đã được xác minh của khách.

Nhân viên được chọn số kg và tỷ lệ thấp hơn khách yêu cầu. Duyệt thấp hơn hoặc từ chối bắt buộc có lý do cụ thể.

Quyết định thủ công của từng sản phẩm được lưu dưới dạng đề xuất cho đến khi mọi sản phẩm đã có đề xuất hoặc kết quả tự duyệt. `DecisionService` tính tạm điều kiện và số tiền hoàn phí vận chuyển từ các đề xuất hiện tại cùng quyết định cuối cùng của yêu cầu trước, sau đó cộng nguyên tử toàn bộ tiền sản phẩm và phí vận chuyển tạm tính để kiểm tra thẩm quyền. Việc duyệt từng sản phẩm ở nhiều thời điểm khác nhau không được làm giảm tổng dùng để kiểm tra thẩm quyền.

Hệ thống tạo nhiệm vụ phê duyệt cấp yêu cầu khi tổng tiền đạt ngưỡng quản lý đang có hiệu lực, mặc định từ `500.000₫`, hoặc mức hỗ trợ tài khoản yêu cầu quản lý xem lại. Hệ thống chưa tạo khoản hoàn trong thời gian chờ. Bất kỳ người nào đã đề xuất trong version gói hiện tại đều không được phê duyệt. Quản lý chấp nhận toàn bộ gói hoặc trả lại để nhân viên xem xét; mọi hành động đều phải có lý do. Khi chấp nhận, hệ thống chốt từng sản phẩm và phí vận chuyển trong cùng giao dịch. Nếu không có điều kiện nào yêu cầu quản lý, hệ thống chốt toàn bộ gói trong cùng giao dịch.

Trước khi chốt một sản phẩm được duyệt, nguyên nhân và bên chịu chi phí là bắt buộc. Luồng tự duyệt dùng nguyên nhân từ sự cố nội bộ đã xác nhận; nếu không có, hệ thống ghi nguyên nhân `Unknown`, bên chịu chi phí `Merchant` và không tự hoàn phí vận chuyển. Với sản phẩm bị từ chối, lý do quyết định là bắt buộc; nguyên nhân có thể là `CustomerFault` hoặc `Unknown`, còn bên chịu chi phí là `None`.

## 13. Bổ sung thông tin và hết hạn

- Cửa hàng chỉ yêu cầu nội dung liên quan trực tiếp đến sản phẩm và đơn hàng.
- Khách có `24 giờ` theo cấu hình để bổ sung.
- Tệp quét lỗi hoặc không an toàn không được tính là đã bổ sung thành công.
- Khi khách bổ sung đủ, sản phẩm quay lại `UnderReview` và bắt đầu SLA quyết định `24 giờ`.
- Khi hết hạn, sản phẩm chuyển `Expired`.
- Số kg đã yêu cầu của `OrderItemId` hết hạn vẫn được tính là đã xử lý và không được đưa vào yêu cầu mới; phần kg chưa từng yêu cầu vẫn còn quyền khiếu nại nếu chưa quá hạn chính sách.
- Quản lý chỉ được mở lại chính sản phẩm đã hết hạn tối đa một lần, không tạo bản sao mới, và phải ghi lý do vào audit.
- Nếu khách vẫn cần bổ sung, sản phẩm quay lại `AwaitingCustomerInfo` với hạn mới theo snapshot chính sách. Nếu thông tin đã đủ, sản phẩm chuyển sang `UnderReview` và yêu cầu bắt đầu SLA quyết định `24 giờ`.
- Số kg vẫn được giữ trong suốt thời gian hết hạn và mở lại; thao tác này không tạo hoặc giải phóng thêm số kg.

## 14. Kháng nghị

- Chỉ sản phẩm ở trạng thái `RejectedPendingAppeal` mới được kháng nghị.
- Mỗi sản phẩm được kháng nghị tối đa một lần.
- Hạn mặc định là `24 giờ` từ lúc quyết định từ chối được gửi cho khách.
- Khách phải cung cấp ít nhất một ảnh/video được tải sau quyết định từ chối và có checksum chưa từng liên kết với sản phẩm đó. Hệ thống không dùng AI để so sánh nội dung.
- Kháng nghị mở lại cùng sản phẩm trong cùng yêu cầu; không tạo yêu cầu mới.
- Quyết định cũ vẫn nằm trong lịch sử.
- Nếu khách không kháng nghị đúng hạn, sản phẩm chuyển sang `Rejected`.
- Cửa hàng phải ra quyết định trong `24 giờ` từ lúc nhận kháng nghị hợp lệ.
- Sau lần xem xét lại, quyết định mới là quyết định cuối cùng và không mở thêm thời hạn kháng nghị. Nếu tiếp tục từ chối, sản phẩm chuyển thẳng sang `Rejected`.
- Sản phẩm đã được duyệt thấp hơn yêu cầu không thuộc luồng kháng nghị của giai đoạn này.
- Hệ thống chờ mọi thời hạn kháng nghị kết thúc trước khi tạo một khoản hoàn tổng hợp.

## 15. Hoàn phí vận chuyển

Phí vận chuyển chỉ được hoàn toàn bộ khi:

- Mỗi dòng hàng trong đơn có ít nhất `1 kg` được chấp nhận ở một tỷ lệ lớn hơn `0%`; không bắt buộc toàn bộ số kg của từng dòng đều hư.
- Mọi phần thiệt hại được dùng để xác lập điều kiện trên đều có nguyên nhân thuộc cửa hàng hoặc đơn vị vận chuyển.
- Không có phần thiệt hại được chấp nhận trong đơn mang nguyên nhân khách hàng hoặc chưa xác định.

Không hoàn phí theo tỷ lệ từng sản phẩm.

Trước khi gói quyết định được chốt, hệ thống xét kết quả đề xuất của yêu cầu hiện tại cùng quyết định cuối cùng của yêu cầu trước trên cùng đơn hàng để tính tạm điều kiện phí vận chuyển. Phí tạm tính được đưa vào tổng kiểm tra thẩm quyền. Khi gói được chốt, quyết định sản phẩm và phí vận chuyển được lưu nguyên tử. Phí vận chuyển chỉ được hoàn thành công một lần cho mỗi đơn hàng; hệ thống phải tính cả khoản phí đã tạo, đang xử lý hoặc đã thành công trước khi duyệt lần mới.

Số tiền được hoàn là phí vận chuyển khách thực trả sau ưu đãi liên quan đến vận chuyển, trừ phần phí đã được hoàn trước và không vượt phần phí vận chuyển còn lại của đơn.

Khi duyệt phí vận chuyển, hệ thống lưu snapshot các quyết định sản phẩm và nguyên nhân đã dùng để xác lập điều kiện. Thay đổi nguyên nhân sau đó chỉ phục vụ đối soát chi phí nội bộ, không tự động tạo thêm hoặc thu hồi khoản hoàn của khách.

Trong luồng tự duyệt, phí vận chuyển chỉ được thêm khi hệ thống đã có sự cố giao nhận được xác nhận nội bộ và cả ba điều kiện trên đều đúng. Tổng bồi thường gồm phí vận chuyển vẫn phải nằm trong thẩm quyền phê duyệt tương ứng.

## 16. Hoàn tiền

### 16.1. Tạo khoản hoàn

Sau khi mọi sản phẩm có quyết định cuối cùng:

- Nếu không sản phẩm nào được duyệt, không tạo khoản hoàn.
- Nếu có sản phẩm được duyệt, hệ thống cộng tiền từng sản phẩm và phí vận chuyển được duyệt.
- Hệ thống tạo một khoản hoàn tổng hợp cho yêu cầu.
- Idempotency key của khoản hoàn gắn ổn định với yêu cầu.
- Module giữ nguyên kiểm soát tài chính hiện có nhưng lấy ngưỡng từ cấu hình: khi khoản hoàn đạt ngưỡng tách nhiệm vụ hiệu lực, mặc định `500.000₫`, người thực thi giao dịch phải khác người tạo khoản hoàn.

### 16.2. Phương thức

- Ưu tiên phương thức thanh toán gốc nếu kênh thanh toán hỗ trợ hoàn tiền.
- Nếu đơn dùng COD, chuyển khoản trực tiếp hoặc kênh gốc không hỗ trợ hoàn, khách cung cấp tài khoản ngân hàng.
- SLA `3 ngày làm việc` chỉ bắt đầu khi yêu cầu đã chốt tài chính, nghĩa là không còn sản phẩm chờ quyết định, phê duyệt hoặc kháng nghị, và thông tin nhận tiền đã đầy đủ.

### 16.3. Thất bại và thử lại

- Lỗi từ kênh thanh toán được phân thành tạm thời hoặc vĩnh viễn.
- Lỗi tạm thời chuyển khoản hoàn sang trạng thái thất bại có thể thử lại. Số lần thử tối đa là cấu hình vận hành, mặc định `3`.
- Thử lại dùng cùng idempotency key và không tạo khoản hoàn mới.
- Nếu kênh gốc báo lỗi vĩnh viễn hoặc hết số lần thử, cùng khoản hoàn chuyển sang chờ thông tin tài khoản ngân hàng; hệ thống không tạo khoản hoàn thứ hai.
- Khi chuyển phương thức, refund ở `AwaitingDestination`, yêu cầu cha ở `ResolutionPending`, khách nhận thông báo cập nhật thông tin nhận tiền và tác vụ xuất hiện trong hàng đợi chờ khách.
- Khi khách gửi tài khoản hợp lệ, refund chuyển sang hàng đợi tài chính theo luồng `AwaitingApproval` rồi `Processing`; yêu cầu cha tiếp tục ở `ResolutionPending`.
- Nếu thông tin tài khoản sai, khách được yêu cầu cập nhật; thời gian chờ khách không tính vào SLA xử lý của bộ phận tài chính.
- SLA tiếp tục tính khi khách đã cung cấp thông tin ngân hàng hợp lệ; thời gian xử lý trước khi chuyển phương thức vẫn được tính, chỉ thời gian chờ khách được tạm dừng.
- Quyết định sản phẩm không bị mất khi hoàn tiền thất bại.

## 17. Xử lý hàng và tồn kho

### 17.1. Hàng hư

Khi một phần hàng hư được duyệt, hệ thống tự ghi `DisposedByCustomer` với khối lượng tương đương:

`Khối lượng xử lý = số kg được duyệt × tỷ lệ được duyệt`

Khách không phải gửi hàng về. Hệ thống không cộng phần này vào tồn kho.

### 17.2. Giao sai

Khách được hoàn tiền cho hàng đã đặt và được giữ hàng giao nhầm. Hệ thống ghi `CustomerKeptWrongItem`. Không có thao tác hoàn kho.

### 17.3. Giao thiếu

Hệ thống ghi `NoPhysicalItem` vì không có hàng vật lý cần xử lý.

## 18. Nguyên nhân và bên chịu chi phí

Hai quyết định được lưu riêng.

Nguyên nhân có thể gồm:

- Cửa hàng đóng sai.
- Cửa hàng đóng thiếu.
- Chất lượng hàng trước giao không đạt.
- Đóng gói không phù hợp.
- Đơn vị vận chuyển giao chậm.
- Đơn vị vận chuyển va đập hoặc bảo quản sai.
- Khách bảo quản hoặc sử dụng sai.
- Không xác định rõ.

Bên chịu chi phí có thể là:

- Cửa hàng.
- Đơn vị vận chuyển.
- Khách hàng.
- Chia sẻ theo quyết định quản lý.
- Không phát sinh chi phí bồi thường.

Quyền lợi khách không chờ kết quả đối soát với đơn vị vận chuyển.

Đối với sản phẩm được duyệt, hai trường này phải có giá trị trước khi chốt gói quyết định. Nếu tự duyệt mà không có sự cố nội bộ xác định nguyên nhân, hệ thống ghi `Unknown` và tạm ghi cửa hàng chịu chi phí. Đối với sản phẩm bị từ chối, hệ thống có thể ghi `Unknown` và `None` nếu không phát sinh bồi thường.

Nhân viên có quyền được điều chỉnh nguyên nhân hoặc bên chịu chi phí sau quyết định để phục vụ đối soát nội bộ. Thay đổi phải có lý do và audit, không làm thay đổi khoản tiền khách đã được duyệt.

## 19. Kiểm soát hành vi bất thường

Số lần khiếu nại cao không tự động chứng minh gian lận. Hệ thống và nhân viên phải dựa vào các sự kiện đã được xác minh.

Hệ thống chỉ tạo tín hiệu rủi ro và đề xuất mức, không tự động hạn chế tài khoản. Nhận xét về nội dung ảnh, chẳng hạn ảnh không rõ, chỉ do nhân viên nhập sau khi xem.

| Mức | Tác động | Điều kiện tối thiểu | Người có quyền áp dụng |
| --- | --- | --- | --- |
| `Nhắc nhở` | Không hạn chế quyền gửi yêu cầu | Thông tin chưa đủ nhưng chưa có căn cứ gian lận | Nhân viên xem xét |
| `Tăng xác minh` | Không tự duyệt; mọi yêu cầu được xem thủ công | Một sự kiện bất nhất đã được xác minh và liên kết trong audit | Người quản lý rủi ro |
| `Hạn chế hỗ trợ nhanh` | Không tự duyệt; mọi quyết định cần quản lý xem lại, không phụ thuộc số tiền | Hai sự kiện bất nhất đã được xác minh trên hai yêu cầu khác nhau | Người quản lý rủi ro |
| `Tạm ngưng tự phục vụ trả hàng` | Khách không tự tạo yêu cầu; CSKH có thể tạo thay và quản lý quyết định | Hai sự kiện gian lận có chủ ý trên hai yêu cầu khác nhau | Quản lý đề xuất và một quản lý khác phê duyệt |

Hai mức giữa bắt buộc có ngày kết thúc. Hết hạn thì hệ thống tự gỡ mức, trừ khi có quyết định mới. Mức cao nhất có thể có ngày kết thúc hoặc được quản lý gỡ bằng một quyết định có lý do. Việc khóa đăng nhập, khóa mua hàng hoặc khóa toàn bộ tài khoản thuộc module Identity và không nằm trong thiết kế Returns này.

Checksum trùng hoặc một lần mô tả chưa nhất quán chỉ làm tăng mức kiểm tra cho yêu cầu hiện tại. Nội dung gửi cho khách phải nói rõ điểm cần bổ sung hoặc lý do từ chối, không dùng câu chung chung như `nghi ngờ gian lận`.

## 20. Xử lý lỗi và cạnh tranh dữ liệu

- Gửi lại cùng idempotency key trả về yêu cầu đã tạo.
- Hai yêu cầu đồng thời phải được kiểm tra trong giao dịch có mức cô lập đủ để không giữ chỗ vượt số kg đã mua.
- Xung đột phiên bản khi nhân viên quyết định trả về thông báo tải lại, không ghi đè quyết định mới hơn.
- Thiếu hoặc sai cấu hình tự duyệt làm hệ thống chuyển sang xem xét thủ công.
- Không có chính sách trả hàng đang hiệu lực làm sản phẩm không đủ điều kiện; giao diện hướng dẫn khách liên hệ CSKH.
- Tệp đang chờ quét, quét lỗi hoặc bị từ chối không thể kích hoạt tự duyệt.
- Lỗi dịch vụ hoàn tiền giữ yêu cầu ở trạng thái có thể thử lại.
- Mọi lý do từ chối phải cụ thể và an toàn để hiển thị cho khách.

## 21. Bảo mật và quyền

Các quyền cần tách riêng:

- Quản lý chính sách trả hàng.
- Xem xét yêu cầu.
- Phê duyệt cấp quản lý.
- Xử lý hoàn tiền.
- Quản lý mức hỗ trợ tài khoản.
- Xem tệp nội bộ.

Thông tin tài khoản nhận tiền được mã hóa. Chỉ bộ phận tài chính có quyền xem đầy đủ và hệ thống xóa phần nhạy cảm sau khi hoàn thành, chỉ giữ thông tin che bớt cần thiết cho đối soát.

Khách không được xem ghi chú rủi ro, ghi chú tài chính, tệp nội bộ hoặc dữ liệu của yêu cầu khác.

## 22. Audit và vận hành

Audit bất biến phải lưu:

- Chính sách và phiên bản được áp dụng.
- Ảnh/video được thêm hoặc bị từ chối ở bước quét.
- Kết quả đánh giá rủi ro và mã quy tắc.
- Yêu cầu bổ sung thông tin.
- Đề xuất, quyết định và phê duyệt.
- Thay đổi nguyên nhân hoặc bên chịu chi phí.
- Kháng nghị.
- Tạo, thử lại, thất bại và hoàn tất khoản hoàn.
- Thay đổi mức hỗ trợ tài khoản.

Dashboard hàng đợi cần có:

- Mới tiếp nhận.
- Cần khách bổ sung.
- Cần thông tin nhận tiền.
- Đang xem xét.
- Chờ quản lý.
- Chờ hoàn tiền.
- Hoàn tiền thất bại.
- Quá SLA.

## 23. Kiểm thử bắt buộc

### 23.1. Chính sách và điều kiện

- Ưu tiên sản phẩm, danh mục, mặc định.
- Chính sách đúng lý do và đúng khoảng hiệu lực.
- Không cho tạo khoảng hiệu lực chồng nhau; dữ liệu cũ chồng thời gian được phân giải theo version rồi thời điểm hiệu lực.
- Yêu cầu nhiều sản phẩm dùng trần tự duyệt thấp nhất trong các snapshot chính sách.
- Yêu cầu giữ snapshot khi chính sách đổi.
- Thời điểm đúng hạn cuối hợp lệ; sau hạn không hợp lệ.
- Số kg yêu cầu tối thiểu là `1`; số kg duyệt bằng `0` chỉ hợp lệ khi từ chối.
- Số kg mua, số kg giữ chỗ và số kg đã xử lý.

### 23.2. Tính tiền

- Các mức `25/50/75/100%`.
- Không chấp nhận tỷ lệ ngoài tập được bật; giao thiếu và giao sai luôn là `100%`.
- Chính sách rau củ quả không hợp lệ nếu tắt `AllowPartialRefund` hoặc `AllowFullRefund`.
- Nhiều kg và nhiều sản phẩm.
- Khuyến mãi sản phẩm, combo và giảm giá cấp đơn.
- Làm tròn VND và phân bổ phần lẻ ổn định.
- Trừ khoản hoàn thành công, bỏ qua khoản thất bại hoặc đã hủy.
- Không vượt tổng tiền đã thanh toán.

### 23.3. Tự duyệt và rủi ro

- Giá trị đúng trần tự duyệt hiệu lực và ngay trên ngưỡng, gồm giá trị mặc định `100.000₫` và một giá trị đã cấu hình lại.
- Tỷ lệ đúng trần hiệu lực và ngay trên ngưỡng, gồm tỷ lệ mặc định `30%` và một tỷ lệ đã cấu hình lại.
- Tổng nhiều sản phẩm không thể lách trần.
- Nhiều yêu cầu trên cùng đơn không thể lách trần.
- Tệp trùng trong cùng yêu cầu được phép liên kết nhiều sản phẩm.
- Tệp xuất hiện ở yêu cầu khác chặn tự duyệt.
- Tài khoản ở mức tăng xác minh bị loại khỏi tự duyệt.
- Cấu hình thiếu làm chuyển thủ công.
- Hậu kiểm chọn đúng tỷ lệ cấu hình mà không dùng kết quả để đảo ngược khoản hoàn đã hoàn tất.

### 23.4. Quyết định và kháng nghị

- Duyệt toàn bộ, duyệt thấp hơn và từ chối từng sản phẩm.
- Bắt buộc lý do khi duyệt thấp hơn hoặc từ chối.
- Các quyết định sản phẩm được cộng nguyên tử trước khi kiểm tra thẩm quyền cấp yêu cầu.
- Phí vận chuyển tạm tính được đưa vào tổng thẩm quyền trước khi chốt gói.
- Khoản đúng ngưỡng quản lý hiệu lực cần quản lý phê duyệt, gồm ngưỡng mặc định `500.000₫` và một ngưỡng đã cấu hình lại.
- Yêu cầu dưới ngưỡng tiền vẫn cần quản lý khi mức hỗ trợ tài khoản quy định như vậy.
- Người đề xuất không thể tự phê duyệt.
- Người tạo khoản hoàn đạt ngưỡng tách nhiệm vụ tài chính hiệu lực không thể tự thực thi giao dịch.
- Chỉ kháng nghị một lần, đúng hạn và có ảnh/video mới.
- Duyệt thấp hơn yêu cầu không mở luồng kháng nghị trong giai đoạn này.
- Quyết định cũ còn nguyên sau kháng nghị.
- Yêu cầu ban đầu đủ thông tin, yêu cầu sau khi mọi bổ sung kết thúc và yêu cầu được mở lại do kháng nghị đều tuân thủ SLA quyết định `24 giờ`.

### 23.5. Hoàn tiền và hàng hóa

- Một khoản hoàn tổng hợp cho nhiều sản phẩm.
- Hoàn về phương thức gốc và chuyển khoản dự phòng.
- Lỗi vĩnh viễn hoặc hết số lần thử trên kênh gốc chuyển cùng khoản hoàn sang chờ tài khoản ngân hàng.
- Retry không tạo khoản hoàn thứ hai.
- Hoàn phí vận chuyển xét mọi dòng hàng, các yêu cầu trước, nguyên nhân của từng phần và khoản phí đã xử lý trước.
- `DisposedByCustomer`, `CustomerKeptWrongItem` và `NoPhysicalItem` không làm tăng tồn kho.

### 23.6. Bảo mật và tích hợp

- Quyền xem ảnh/video của khách và tệp nội bộ.
- Quyền xem thông tin tài khoản nhận tiền.
- Dữ liệu nhạy cảm được xóa sau khi hoàn tất.
- Integration test SQL Server cho giữ chỗ số kg, quyết định đồng thời và idempotency hoàn tiền.
- Nội dung khách nhìn thấy không để lộ ghi chú rủi ro hoặc tài chính.
- Chỉ đúng vai trò mới được đặt, gia hạn, hạ mức hoặc gỡ hạn chế hỗ trợ tài khoản.

## 24. Chuyển đổi dữ liệu hiện có

Hệ thống đã có dữ liệu yêu cầu trả hàng, nên migration phải giữ nguyên lịch sử:

- Sản phẩm cũ không có tỷ lệ yêu cầu được gán `100%`. Tỷ lệ duyệt được gán `100%` khi `ApprovedQuantity > 0`, ngược lại để trống.
- Số lượng cũ của nhóm rau củ quả được hiểu là kg nguyên.
- Bản ghi xử lý hàng cũ có khối lượng tương đương bằng số lượng cũ.
- Chính sách mới được tạo thành phiên bản mới; không sửa snapshot của yêu cầu cũ.

Trạng thái sản phẩm cũ được ánh xạ như sau:

| Trạng thái yêu cầu cũ | Trạng thái sản phẩm mới |
| --- | --- |
| `Submitted` | `Submitted` |
| `AwaitingEvidence` | `AwaitingCustomerInfo` |
| `UnderReview` | `UnderReview` |
| `Approved` | `Approved` nếu `ApprovedQuantity > 0`; trường hợp khác là dữ liệu lỗi |
| `PartiallyApproved` | `Approved` nếu `ApprovedQuantity > 0`, kèm sự kiện `PartiallyApproved`; `Rejected` nếu bằng `0` |
| `Rejected` | `Rejected` và không mở thời hạn kháng nghị mới |
| `ResolutionPending`, `ResolutionFailed`, `Resolved` | `Approved` nếu `ApprovedQuantity > 0`; `Rejected` nếu bằng `0` |
| `Cancelled` | `Cancelled` |
| `Expired` | `Expired` |

Migration tạo một sự kiện cho mỗi kết quả suy ra. Trước khi đổi schema, lệnh kiểm tra dữ liệu phải dừng migration và xuất danh sách cần sửa nếu gặp `RequestedQuantity <= 0`, `ApprovedQuantity < 0`, `ApprovedQuantity > RequestedQuantity`, trạng thái không nhận biết hoặc yêu cầu `Approved` không có sản phẩm được duyệt. Hệ thống không tự đoán đối với bản ghi lỗi.

Yêu cầu đang mở tiếp tục theo trạng thái và snapshot hiện có, chỉ dùng trường mới sau khi migration hoàn tất thành công.

## 25. Tiêu chí hoàn thành

Thiết kế được triển khai đúng khi:

- Khách có thể gửi nhiều sản phẩm và chọn số kg cùng mức hư hỏng cho từng sản phẩm.
- Hệ thống không chấp nhận số kg vượt phần còn lại.
- Luồng tự duyệt chỉ chạy khi đồng thời đạt trần tiền, tỷ lệ và mọi điều kiện an toàn.
- Nhân viên và quản lý bị giới hạn đúng theo thẩm quyền cấu hình.
- Tiền hoàn khớp giá trị thực trả và không thể hoàn trùng.
- Kháng nghị chỉ diễn ra một lần trong cùng yêu cầu.
- Hàng hư, giao sai và giao thiếu được ghi nhận đúng mà không tăng tồn kho.
- Khách nhận được câu chữ tự nhiên, cụ thể và không mang tính cáo buộc.
- Dashboard cho phép đổi chính sách bằng phiên bản mới mà không ảnh hưởng yêu cầu cũ.
- Các ca kiểm thử ở mục 23 đều đạt.
