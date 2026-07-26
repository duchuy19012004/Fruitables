---
type: d2-erd-index
feature: price-schedule
updated: 2026-07-17
---

# Price schedule — D2 ERD Index

**Phạm vi:** chỉ các bảng liên quan chức năng Quản lý giá (giá gốc đơn lẻ + lịch giảm giá tự động).  
**File:** [price-schedule.d2](./price-schedule.d2) · [price-schedule.svg](./price-schedule.svg)  
**Regen:** `node .agents/scripts/d2-render.mjs docs/price-schedule/d2-erd/price-schedule.d2`

| Bảng | Vai trò trong quản lý giá | PK | FK ra |
|------|--------------------------|----|-------|
| Products | Giá gốc sản phẩm (chỉ sửa qua PriceManagementService) | Mã | Categories |
| ProductVariants | Giá gốc từng phân loại | Mã | Products |
| PriceSchedules | Lịch giảm giá theo thời gian (Chờ → Đang chạy → Kết thúc) | Mã | Products, ProductVariants, Users (admin tạo) |
| ProductLogs | Nhật ký mọi thay đổi giá (đơn lẻ / hàng loạt / lịch) | Mã | Products, Users (admin) |
| Users (cột chính) | Admin tạo lịch / ghi nhật ký | Mã | — |
| Categories (cột chính) | Nhóm của sản phẩm | Mã | Categories (tự tham chiếu) |

**Ghi chú:**
- `SalePrice`, `DisplayMinPrice`, `DisplayMaxPrice` trên `Product` và `SalePrice`, `DisplayPrice` trên `ProductVariant` là thuộc tính `[NotMapped]` (cache hiển thị tính từ lịch đang chạy) → **không phải cột DB**, không xuất hiện trên ERD.
- Giá hiệu lực không lưu DB — được tính động từ `Products.Price` / `ProductVariants.Price` + `PriceSchedules` đang hiệu lực; hết hạn giá tự quay về giá gốc.
