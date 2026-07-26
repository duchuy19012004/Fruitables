---
type: d2-erd-index
feature: chatbox
updated: 2026-07-17
---

# Chatbox — D2 ERD Index

**Phạm vi:** chỉ các bảng liên quan chức năng Chatbox (chat AI hỗ trợ khách hàng).  
**File:** [chatbox.d2](./chatbox.d2) · [chatbox.svg](./chatbox.svg)  
**Regen:** `node .agents/scripts/d2-render.mjs docs/chatbox/d2-erd/chatbox.d2`

| Bảng | Vai trò trong chatbox | PK | FK ra |
|------|----------------------|----|-------|
| ChatSessions | Phiên hội thoại của khách | Mã (GUID) | Users |
| ChatMessages | Tin nhắn trong phiên | Mã (số lớn) | ChatSessions |
| KnowledgeChunks | Tri thức RAG (embedding) | Mã (số lớn) | — (tham chiếu mềm tới Faqs/Products) |
| Faqs | Nguồn tri thức hỏi đáp | Mã | — |
| Users (cột chính) | Chủ phiên chat | Mã | — |
| Products (cột chính) | Nguồn tri thức sản phẩm | Mã | Categories |

**Ghi chú:** `KnowledgeChunks.Mã nguồn` trỏ mềm tới `Faqs.Mã` hoặc `Products.Mã` theo `Loại nguồn` — vẽ bằng đường đứt nét, không phải khóa ngoại thật. `Users`/`Products` chỉ giữ các cột chính liên quan chatbox.
