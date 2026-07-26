---
type: srs-flows
feature: chatbox
updated: 2026-07-16
---

# Chatbox — Flows

## Flow: Người dùng sử dụng chatbox (UML Activity Swimlane)
**Trigger**: Người dùng mở widget hoặc trang Chat hỗ trợ.
**Related UC**: TBD
**Related FR**: TBD
**Related E**: TBD

**Phong cách**: UML activity swimlane PlantUML — **một luồng tổng quát**, chia nhỏ **lane** (không chia phase).

- HTML: [chatbox-user-flow.html](./chatbox-user-flow.html)
- Source: [chatbox-user-flow-swimlane.puml](./chatbox-user-flow-swimlane.puml)
- SVG: [chatbox-user-flow-swimlane.svg](./chatbox-user-flow-swimlane.svg)
- PNG: [chatbox-user-flow-swimlane.png](./chatbox-user-flow-swimlane.png)

**Lane** (tách “Hệ thống Chatbox”, nhãn bước mức nghiệp vụ):

| Lane | Vai trò |
|------|---------|
| Người dùng | Mở chat, hỏi, đọc, đóng |
| Giao diện Chat | Nhớ cuộc chat, hiện tin, gửi tin, cập nhật câu trả lời |
| API Chat | Mở cuộc chat, nhận tin, ghi nhận, điều phối trả lời |
| RAG / AI | Tìm thông tin liên quan, viết câu trả lời, gửi dần |

**Phạm vi**: mở chat → **hiển thị khung** → mở cuộc chat → gửi tin → soạn trả lời → hiện màn hình → hỏi tiếp hoặc đóng.  
**Decision (3)**: mở được không · nhận tin được không · hỏi tiếp không.  
**Regen**: `node .agents/scripts/plantuml-render.mjs docs/chatbox/srs/chatbox-user-flow-swimlane.puml --png`

## Flow: Người dùng sử dụng chatbox (Activity Mermaid)
**Trigger**: Người dùng mở widget hoặc trang Chat hỗ trợ.
**Related UC**: TBD
**Related FR**: TBD
**Related E**: TBD

```mermaid
flowchart TB
    subgraph USER["Người dùng"]
        U0((Bắt đầu))
        U1[Mở widget hoặc trang Chat]
        U2[Chọn câu hỏi gợi ý hoặc nhập nội dung]
        U3[Nhấn Gửi]
        U4[Đọc câu trả lời hoặc thông báo]
        U5{Tiếp tục trò chuyện?}
        U6[Đóng chat]
        U7((Kết thúc))
    end

    subgraph CHAT["Hệ thống Chatbox Fruitables"]
        S1[Kiểm tra phiên chat đã lưu]
        D1{Có mã phiên đã lưu?}
        S2[Tải lịch sử hội thoại]
        D2{Phiên còn được phép truy cập?}
        S3[Xóa mã phiên cũ]
        S4[Tạo phiên chat mới]
        D3{Tạo phiên thành công?}
        S5[Hiển thị lời chào và lịch sử nếu có]
        D4{Nội dung hợp lệ?}
        S6[Hiển thị tin người dùng và trạng thái đang trả lời]
        D5{Yêu cầu được chấp nhận?}
        S7[Tra cứu tri thức và tạo phản hồi]
        D6{Có câu trả lời phù hợp?}
        S8[Stream câu trả lời]
        S9[Stream thông báo từ chối]
        S10[Hiển thị lỗi nội dung, giới hạn gửi hoặc lỗi hệ thống]
        D7{Stream hoàn tất?}
        S11[Hoàn tất và lưu câu trả lời]
        S12[Giữ phần đã nhận và báo lỗi]
        S13[Thông báo không thể tạo phiên]
    end

    U0 --> U1 --> S1 --> D1
    D1 -->|Có| S2 --> D2
    D1 -->|Không| S4
    D2 -->|Có| S5
    D2 -->|Không| S3 --> S4
    S4 --> D3
    D3 -->|Có| S5
    D3 -->|Không| S13 --> U6
    S5 --> U2 --> D4
    D4 -->|Không| U2
    D4 -->|Có| U3 --> S6 --> D5
    D5 -->|Không| S10 --> U4
    D5 -->|Có| S7 --> D6
    D6 -->|Có| S8 --> D7
    D6 -->|Không| S9 --> D7
    D6 -->|Lỗi| S10
    D7 -->|Có| S11 --> U4
    D7 -->|Không| S12 --> U4
    U4 --> U5
    U5 -->|Có| U2
    U5 -->|Không| U6 --> U7
```
