# Nội dung thuyết trình: Hệ thống thương mại điện tử rau củ quả B2C

## Slide bìa, không tính trong 15 slide

### Nội dung trên slide

Xây dựng hệ thống thương mại điện tử rau củ quả theo mô hình B2C

Fruitables

Họ tên sinh viên, giảng viên hướng dẫn, lớp và năm thực hiện.

### Ghi chú hình ảnh

Dùng một ảnh rau củ quả rõ nét làm hình chính. Không đưa sơ đồ hoặc danh sách chức năng lên slide bìa.

## Slide 1. Fruitables đưa hoạt động bán rau củ quả lên một hệ thống thống nhất

### Nội dung trên slide

- Fruitables là website thương mại điện tử dành cho cửa hàng rau củ quả.
- Cửa hàng bán trực tiếp cho người tiêu dùng theo mô hình B2C.
- Khách có thể tìm sản phẩm, đặt hàng, thanh toán và theo dõi đơn hàng.
- Chủ cửa hàng và nhân viên quản lý hoạt động bán hàng trên cùng hệ thống.

### Lời thuyết trình gợi ý

Đề tài của nhóm là Fruitables, một hệ thống thương mại điện tử chuyên cho rau củ quả. Hệ thống kết nối trực tiếp cửa hàng với người mua cuối, vì vậy mô hình được lựa chọn là B2C. Phạm vi không dừng ở trang bán hàng mà còn có phần quản trị để cửa hàng theo dõi sản phẩm, đơn hàng, giá bán và các chương trình kinh doanh.

### Ghi chú hình ảnh

Nên dùng một ảnh chụp giao diện trang chủ hoặc trang danh sách sản phẩm. Nếu chưa có ảnh đẹp, tạm dùng sơ đồ đơn giản: Cửa hàng, Fruitables, Khách hàng.

## Slide 2. Việc bán hàng rời rạc làm cả khách mua lẫn cửa hàng mất thời gian

### Nội dung trên slide

- Khách khó tìm đúng sản phẩm, mức giá và phân loại phù hợp.
- Thông tin sản phẩm, tồn kho và giá bán dễ thiếu đồng bộ.
- Cửa hàng phải xử lý đơn, đánh giá và tài khoản ở nhiều nơi.
- Khách cần được hỗ trợ nhanh khi có câu hỏi trước khi mua.

### Lời thuyết trình gợi ý

Trong cách bán hàng thủ công, thông tin thường nằm rải rác ở tin nhắn, bảng tính hoặc nhiều kênh khác nhau. Khách phải hỏi lại về giá, phân loại và tình trạng hàng. Ở phía cửa hàng, việc thay đổi giá hoặc theo dõi đơn hàng cũng dễ sai nếu dữ liệu không được quản lý tập trung. Fruitables được xây dựng để giải quyết chính những điểm này.

### Ghi chú hình ảnh

Không cần dùng use case ở slide này. Có thể minh họa bằng hành trình ngắn gồm bốn điểm: tìm hàng, hỏi giá, đặt đơn, chờ xác nhận.

## Slide 3. Giải pháp bao phủ trọn quy trình mua hàng B2C

### Nội dung trên slide

- Trưng bày sản phẩm, tìm kiếm, lọc và giỏ hàng.
- Đặt hàng, thanh toán, giao nhận và theo dõi trạng thái.
- Quản trị sản phẩm, đơn hàng, khách hàng và nội dung bán hàng.
- Chatbox, quản lý giá và combo là ba tính năng được tập trung phát triển.

### Lời thuyết trình gợi ý

Fruitables bao phủ quy trình từ lúc khách bắt đầu tìm sản phẩm đến khi nhận hàng và đánh giá. Dữ liệu bán hàng được dùng chung giữa giao diện khách và khu vực quản trị. Trong phạm vi trình bày này, nhóm tập trung vào ba phần có nhiều xử lý nghiệp vụ hơn là Chatbox, quản lý giá và tạo combo sản phẩm.

### Ghi chú hình ảnh

Nên vẽ một hành trình ngang: Khám phá sản phẩm, giỏ hàng, đặt hàng, giao nhận, chăm sóc sau mua. Chỉ dùng tên ngắn, không đưa chi tiết database lên slide này.

## Slide 4. Hệ thống được nhìn từ hai nhóm sử dụng chính

### Nội dung trên slide

- Nhóm mua hàng: khách vãng lai và khách hàng đã đăng nhập.
- Nhóm vận hành: nhân viên và chủ cửa hàng.
- Khách hàng kế thừa các thao tác cơ bản của khách vãng lai.
- Chủ cửa hàng có phạm vi quản trị rộng hơn nhân viên.

### Lời thuyết trình gợi ý

Các actor trong hệ thống được gom thành hai nhóm để dễ theo dõi. Nhóm đầu tiên trực tiếp tìm và mua hàng. Nhóm thứ hai vận hành cửa hàng, trong đó nhân viên xử lý công việc hằng ngày còn chủ cửa hàng có thêm các quyền cấu hình và kiểm soát. Cách chia này cũng là nền tảng để thiết kế phân quyền.

### Ghi chú hình ảnh

Nên tự dựng một hình tổng quát gồm hai phía, mỗi phía có hai actor. Không paste các sơ đồ chi tiết vì chữ sẽ quá nhỏ.

## Slide 5. Khách có thể bắt đầu mua sắm ngay cả khi chưa đăng nhập

### Nội dung trên slide

- Khách vãng lai tìm kiếm, lọc và xem danh sách sản phẩm.
- Khách có thể xem đánh giá và thêm sản phẩm vào giỏ hàng.
- Sau khi đăng nhập, khách quản lý hồ sơ, đặt hàng và theo dõi đơn.
- Chatbox hỗ trợ cả khách vãng lai và khách hàng.

### Lời thuyết trình gợi ý

Use case tổng quát cho thấy hệ thống không bắt khách đăng nhập ngay từ đầu. Người dùng vẫn có thể xem sản phẩm, tham khảo đánh giá và chuẩn bị giỏ hàng. Khi cần đặt hàng hoặc quản lý thông tin cá nhân, họ sử dụng tài khoản khách hàng. Chatbox được đặt ở cả hai trạng thái để hỗ trợ xuyên suốt quá trình mua.

### Ghi chú hình ảnh

Paste hình `assets/usecase-customer-guest-overview.png` ở bên phải, phần giải thích đặt bên trái. Đây là sơ đồ tổng quát nên không cần đọc từng đường nối trên slide.

## Slide 6. Use case khách vãng lai tập trung vào giai đoạn khám phá sản phẩm

### Nội dung trên slide

- Xem danh sách sản phẩm đang bán.
- Tìm kiếm và lọc theo nhu cầu.
- Xem đánh giá trước khi quyết định.
- Thêm sản phẩm vào giỏ hàng.

### Lời thuyết trình gợi ý

Khách vãng lai chủ yếu thực hiện các thao tác trước khi mua. Họ cần tìm đúng sản phẩm, đọc thông tin và xem trải nghiệm của người mua trước. Hệ thống cho phép thêm hàng vào giỏ để giảm rào cản sử dụng, sau đó mới yêu cầu thông tin cần thiết khi chuyển sang đặt hàng.

### Ghi chú hình ảnh

Paste hình `assets/usecase-guest-detail.png`. Có thể phóng lớn toàn bộ sơ đồ vì hình này ít use case và dễ đọc.

## Slide 7. Tài khoản khách hàng hoàn thiện hành trình từ đặt hàng đến sau mua

### Nội dung trên slide

- Quản lý thông tin cá nhân và địa chỉ nhận hàng.
- Đặt hàng, thanh toán và theo dõi trạng thái.
- Hủy đơn khi đơn còn đáp ứng điều kiện cho phép.
- Đánh giá sản phẩm sau khi mua.

### Lời thuyết trình gợi ý

Sau khi đăng nhập, người dùng có thể hoàn tất đơn hàng và theo dõi quá trình xử lý. Hệ thống lưu hồ sơ để khách không phải nhập lại thông tin ở mỗi lần mua. Quyền hủy đơn phụ thuộc vào trạng thái hiện tại của đơn. Khi giao dịch đã hoàn tất, khách có thể gửi đánh giá cho sản phẩm.

### Ghi chú hình ảnh

Phân rã từ nửa dưới của sơ đồ tổng quát ở slide 5 và dựng lại thành một hành trình ngang. Không nên paste lại cùng một hình vì nội dung khách hàng sẽ bị nhỏ và trùng với slide trước.

## Slide 8. Nhân viên vận hành hằng ngày, chủ cửa hàng kiểm soát toàn bộ hệ thống

### Nội dung trên slide

- Nhân viên quản lý sản phẩm, danh mục, đơn hàng và phản hồi khách.
- Chủ cửa hàng thực hiện được các nghiệp vụ của nhân viên.
- Chủ có thêm quyền quản lý giá, combo, voucher, giao nhận, SEO và phân quyền.
- Mỗi tài khoản chỉ thấy chức năng phù hợp với vai trò được cấp.

### Lời thuyết trình gợi ý

Ở khu vực quản trị, nhân viên xử lý các nghiệp vụ diễn ra thường xuyên như cập nhật sản phẩm và đơn hàng. Chủ cửa hàng cần một góc nhìn rộng hơn để điều chỉnh chính sách bán hàng và quyền truy cập. Hệ thống tách vai trò để giảm thao tác nhầm và giới hạn dữ liệu mà từng tài khoản được phép xử lý.

### Ghi chú hình ảnh

Tạo hình tổng quát hai cột Nhân viên và Chủ cửa hàng. Dùng các sơ đồ chi tiết ở slide 9 và 10 làm nguồn, không paste hai hình dài vào cùng một slide.

## Slide 9. Nhân viên xử lý các nghiệp vụ vận hành và chăm sóc khách hàng

### Nội dung trên slide

- Quản lý sản phẩm, danh mục và chi tiết đơn hàng.
- Lọc đơn theo trạng thái thanh toán, hỗ trợ hủy đơn khi cần.
- Duyệt hoặc ẩn đánh giá, xử lý báo cáo vi phạm.
- Quản lý tài khoản khách hàng và xem thống kê doanh thu.

### Lời thuyết trình gợi ý

Use case của nhân viên tập trung vào công việc vận hành. Nhân viên có thể kiểm tra đơn, xử lý đánh giá và hỗ trợ tài khoản khách hàng. Thống kê doanh thu giúp họ theo dõi tình hình bán hàng trong phạm vi được cấp quyền. Những thao tác ảnh hưởng đến cấu hình chung vẫn dành cho chủ cửa hàng.

### Ghi chú hình ảnh

Paste hình `assets/usecase-employee-detail.png`. Sơ đồ khá dài, nên đặt hình chiếm khoảng hai phần ba slide và dùng hiệu ứng phóng to theo hai vùng khi thuyết trình: vận hành đơn hàng, quản trị khách hàng và báo cáo.

## Slide 10. Chủ cửa hàng điều chỉnh chính sách bán hàng và quyền truy cập

### Nội dung trên slide

- Quản lý giá, combo sản phẩm, voucher và giao nhận.
- Quản lý phân quyền cho tài khoản nội bộ.
- Theo dõi doanh thu, tài khoản khách và các đánh giá vi phạm.
- Quản lý SEO để cải thiện khả năng tìm thấy sản phẩm.

### Lời thuyết trình gợi ý

Chủ cửa hàng có toàn bộ quyền vận hành và thêm các chức năng mang tính cấu hình. Trong đó, quản lý giá và tạo combo tác động trực tiếp đến cách sản phẩm được bán cho khách. Phân quyền giúp chủ kiểm soát nhân viên nào được sử dụng từng nhóm chức năng. Đây cũng là actor chính trong hai tính năng quản lý giá và combo được trình bày tiếp theo.

### Ghi chú hình ảnh

Paste hình `assets/usecase-owner-detail.png`. Nên phân rã khi trình bày thành ba vùng: vận hành chung, kinh doanh và quản trị hệ thống. Không cố đọc toàn bộ use case trong một lần.

## Slide 11. Chatbox trả lời dựa trên dữ liệu thật của cửa hàng

### Nội dung trên slide

- Hỗ trợ cả khách vãng lai và khách hàng đã đăng nhập.
- Tiếp nhận câu hỏi về sản phẩm, chính sách và thông tin cửa hàng.
- Tìm đoạn kiến thức phù hợp trước khi tạo câu trả lời.
- Lưu lịch sử phiên chat để người dùng tiếp tục cuộc trò chuyện.

### Lời thuyết trình gợi ý

Chatbox không chỉ trả lời theo một nội dung cố định. Khi khách gửi câu hỏi, hệ thống tìm trong kho kiến thức được tạo từ FAQ, sản phẩm và cấu hình cửa hàng. Phần nội dung phù hợp được đưa vào quá trình tạo câu trả lời. Mỗi phiên chat và từng tin nhắn đều được lưu để giữ mạch hội thoại và hỗ trợ kiểm tra khi cần.

### Ghi chú hình ảnh

Paste sơ đồ luồng [chatbox-user-flow-swimlane.svg](../chatbox/srs/chatbox-user-flow-swimlane.svg). Nếu sơ đồ quá cao, chỉ lấy đoạn từ lúc khách gửi câu hỏi đến khi giao diện hiển thị câu trả lời.

## Slide 12. Dữ liệu Chatbox tách riêng hội thoại và kho kiến thức

### Nội dung trên slide

- `ChatSessions` lưu phiên chat và liên kết tùy chọn với `Users`.
- `ChatMessages` lưu tin nhắn của khách và trợ lý theo từng phiên.
- `KnowledgeChunks` lưu các đoạn kiến thức và vector tìm kiếm.
- `Faqs` và `Products` là hai nguồn chính để xây dựng kho kiến thức.

### Lời thuyết trình gợi ý

Cơ sở dữ liệu Chatbox có hai nhóm rõ ràng. Nhóm thứ nhất lưu lịch sử giao tiếp qua phiên chat và tin nhắn. Nhóm thứ hai lưu tri thức để hệ thống tìm nội dung liên quan trước khi trả lời. Việc tách hai nhóm giúp lịch sử trò chuyện không phụ thuộc vào cách kho kiến thức được cập nhật.

### Ghi chú hình ảnh

Paste sơ đồ [chatbox.svg](../chatbox/d2-erd/chatbox.svg) ở kích thước lớn. Khi nói, đi theo hai nhánh: `Users -> ChatSessions -> ChatMessages` và `Faqs/Products -> KnowledgeChunks`.

## Slide 13. Quản lý giá cho phép thay đổi giá có lịch và có kiểm soát

### Nội dung trên slide

- Quản lý giá gốc theo sản phẩm hoặc từng phân loại.
- Tạo lịch giảm theo giá cố định hoặc phần trăm.
- Kiểm tra thời gian trùng và giá trị giảm trước khi lưu.
- Ghi nhật ký người thực hiện và cập nhật giá hiển thị cho khách.

### Lời thuyết trình gợi ý

Tính năng quản lý giá xử lý cả giá gốc và các đợt giảm giá theo thời gian. Chủ cửa hàng có thể áp dụng lịch cho sản phẩm thường hoặc cho từng phân loại. Trước khi lưu, hệ thống kiểm tra khoảng thời gian và mức giảm để tránh hai lịch cùng tác động lên một sản phẩm. Sau khi giá thay đổi, dữ liệu hiển thị cho khách và giá tính trong combo cũng được tính lại.

### Ghi chú hình ảnh

Dùng bố cục chia đôi. Bên trái đặt luồng [price-e-create-apply-swimlane.svg](../price-schedule/srs/price-e-create-apply-swimlane.svg), bên phải đặt ERD [price-schedule.svg](../price-schedule/d2-erd/price-schedule.svg). ERD chỉ cần làm nổi các bảng `Products`, `ProductVariants`, `PriceSchedules`, `ProductLogs` và `Users`; bảng `Categories` để mờ hơn. Nếu chữ quá nhỏ, chuyển ERD đầy đủ sang slide phụ lục.

## Slide 14. Combo gom nhiều sản phẩm nhưng vẫn dùng giá và tồn kho hiện tại

### Nội dung trên slide

- Chủ cửa hàng tạo combo từ sản phẩm hoặc phân loại đang bán.
- Mỗi mục combo có số lượng và thứ tự hiển thị riêng.
- Tổng giá được tính từ giá hiệu lực của từng sản phẩm.
- Khi thêm vào giỏ, hệ thống kiểm tra trạng thái và tồn kho từng mục.

### Lời thuyết trình gợi ý

Combo giúp cửa hàng nhóm nhiều mặt hàng thành một gợi ý mua chung. Hệ thống không lưu một tổng giá cố định mà tính lại từ giá đang có hiệu lực của từng sản phẩm. Vì vậy, khi lịch giảm giá thay đổi, giá combo cũng thay đổi theo. Nếu một mục hết hàng, hệ thống nhận diện mục đó trước khi thêm combo vào giỏ.

### Ghi chú hình ảnh

Dùng sơ đồ luồng [combo-product-flow-swimlane.svg](../combo/srs/combo-product-flow-swimlane.svg) làm hình chính. Đặt ERD [combo.svg](../combo/d2-erd/combo.svg) ở góc phải hoặc dùng một sơ đồ rút gọn gồm `Combos -> ComboItems -> Products/ProductVariants`. ERD có bốn bảng nên có thể giữ trên cùng slide nếu chữ vẫn đọc được.

## Slide 15. Fruitables nối trải nghiệm mua hàng với công việc vận hành phía sau

### Nội dung trên slide

- Khách tìm hàng, nhận hỗ trợ, đặt mua và theo dõi đơn trên một website.
- Nhân viên xử lý công việc hằng ngày theo đúng phạm vi được cấp.
- Chủ cửa hàng điều chỉnh giá, combo và chính sách bán hàng từ dữ liệu tập trung.
- Ba phần demo đề xuất: Chatbox, quản lý giá, tạo combo sản phẩm.

### Lời thuyết trình gợi ý

Fruitables giải quyết bài toán ở cả hai phía của mô hình B2C. Khách có một quy trình mua hàng liền mạch, còn cửa hàng có dữ liệu tập trung để vận hành. Chatbox giảm thời gian tìm thông tin, quản lý giá giúp kiểm soát chương trình bán hàng, và combo hỗ trợ bán nhiều sản phẩm theo nhóm. Sau phần tổng quan này, nhóm có thể chuyển sang demo lần lượt ba tính năng chính.

### Ghi chú hình ảnh

Không dùng ERD ở slide kết. Nên ghép ba ảnh giao diện thật của Chatbox, quản lý giá và combo. Nếu chưa có ảnh, dùng ba nhãn lớn kèm một câu kết ở giữa: Một nguồn dữ liệu, hai phía sử dụng.

## Ghi chú chung khi dựng PowerPoint

- Tổng số là 15 slide nội dung, chưa tính slide bìa.
- Các slide 9 và 10 có sơ đồ use case dài, nên dùng hiệu ứng phóng to theo từng vùng hoặc tách phần chi tiết sang phụ lục khi bảo vệ.
- Slide 13 và 14 chứa cả luồng xử lý lẫn database. Nếu chữ nhỏ hơn mức đọc được trên màn chiếu, giữ luồng chính trong bài và chuyển ERD đầy đủ sang phụ lục.
- Không đọc toàn bộ use case trên hình. Chỉ chỉ ra actor, nhóm nghiệp vụ và một vài quan hệ quan trọng.
- Mỗi slide nên giữ tối đa bốn ý chính. Phần giải thích dài để trong speaker notes.
