---
type: d2-erd-index
feature: combo
updated: 2026-07-17
---

# Combo — D2 ERD Index

**Phạm vi:** chỉ các bảng liên quan chức năng Combo sản phẩm.  
**File:** [combo.d2](./combo.d2) · [combo.svg](./combo.svg)  
**Regen:** `node .agents/scripts/d2-render.mjs docs/combo/d2-erd/combo.d2`

| Bảng | Vai trò trong combo | PK | FK ra |
|------|--------------------|----|-------|
| Combos | Đầu combo (tên, ảnh, thứ tự hiển thị) | Mã | — |
| ComboItems | Mục trong combo (sản phẩm + số lượng) | Mã | Combos, Products, ProductVariants |
| Products (cột chính) | Sản phẩm thành phần | Mã | — |
| ProductVariants (cột chính) | Phân loại cụ thể của sản phẩm thành phần (trống được) | Mã | Products |

**Ghi chú:**
- Combo **không lưu giá** — tổng giá combo được tính động từ giá hiệu lực của từng mục (`ComboService` dùng `IProductPricingService`), nên giá combo tự cập nhật khi giá sản phẩm thành phần hoặc lịch giảm giá thay đổi.
- Xóa Combo → xóa theo các mục (Cascade); xóa Sản phẩm/Phân loại đang nằm trong combo bị chặn (Restrict).
