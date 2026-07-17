# Thiết kế: Combo theo món ăn ("Mua theo món")

Ngày: 2026-07-17
Trạng thái: Đã duyệt thiết kế (qua brainstorming), chờ review spec

## 1. Bối cảnh & mục tiêu

Fruitables là web TMĐT B2C bán rau củ/trái cây tươi. Chức năng hiện tại khá phổ thông so với các web khác. Feature này tạo điểm khác biệt: chuyển từ "bán từng món rau" sang "giải quyết cả bữa ăn" — khách chọn một món ăn (vd "Canh chua cá") và thêm toàn bộ nguyên liệu vào giỏ bằng 1 cú bấm.

**Mục tiêu:**

- Admin tạo/sửa/xóa được combo món ăn (CRUD đầy đủ).
- Khách thấy section "Mua theo món" trên trang Shop, thêm cả combo vào giỏ bằng 1 lần bấm.
- Giá combo luôn bằng tổng giá hiện tại của các món, tự động đi theo `PriceSchedule`.

**Không làm (Non-goals, YAGNI):**

- Giảm giá riêng cho combo (v2 nếu cần).
- Trang chi tiết combo riêng (card trên Shop là đủ).
- Gợi ý combo bằng chatbot/AI (v2).
- Unit test project mới (project hiện không có test project — giữ convention).

## 2. Mô hình dữ liệu

Hai bảng mới:

### `Combo`

| Trường | Kiểu | Ghi chú |
|---|---|---|
| Id | int PK | |
| Name | nvarchar(255), required | Tên món ăn, vd "Canh chua cá" |
| Slug | nvarchar(255), required | Tự sinh từ Name, dùng `GenerateSlug` pattern của `ProductAdminService` |
| Description | nvarchar(max), nullable | |
| ImageUrl | nvarchar(500), nullable | Upload qua `IImageUploadService` có sẵn |
| IsActive | bool, default true | |
| SortOrder | int, default 0 | |
| CreatedAt / UpdatedAt | datetime | Theo convention các entity khác |

### `ComboItem`

| Trường | Kiểu | Ghi chú |
|---|---|---|
| Id | int PK | |
| ComboId | int FK → Combo | Cascade delete |
| ProductId | int FK → Product | Restrict delete |
| ProductVariantId | int?, FK → ProductVariant, nullable | Chỉ định biến thể cụ thể nếu cần; Restrict delete |
| Quantity | int, default 1 | |
| SortOrder | int, default 0 | |

**Quyết định chủ chốt:**

- **Không lưu giá combo.** Giá hiển thị = tổng giá realtime từ `IProductPricingService` (đã xử lý PriceSchedule). Không cần đồng bộ giá.
- Combo **không** bị bảng nào khác tham chiếu (Order/Cart lưu từng món riêng), nên admin xóa combo là xóa cứng (ComboItem cascade theo).
- 1 migration `AddMealCombo`.

## 3. Phía Admin (`Areas/Admin/`)

- `ComboController` (Admin area), permission theo cùng cơ chế với Product admin.
- **Index**: bảng danh sách — tên, số món, tổng giá hiện tại, trạng thái, thao tác (Sửa / Xóa / Bật-tắt).
- **Create / Edit**: form gồm:
  - Thông tin combo: Name, Slug (tự sinh, cho phép sửa), Description, ImageUrl (upload), IsActive, SortOrder.
  - Danh sách món: mỗi dòng gồm dropdown chọn sản phẩm (có tìm kiếm), dropdown variant của sản phẩm đó (nếu có), Quantity; nút thêm/xóa dòng bằng JS đơn giản.
  - Validate: tên bắt buộc, combo phải có ít nhất 1 món, Quantity >= 1.
- **Delete**: xóa cứng (cascade ComboItem), có confirm.
- Sidebar admin: thêm mục "Combo món ăn".

## 4. Phía Storefront

- Section **"Mua theo món"** ở đầu trang Shop (`ShopController.Index`), chỉ lấy combo `IsActive` và còn ít nhất 1 món khả dụng, sắp theo SortOrder.
- Mỗi card: ảnh, tên món ăn, danh sách món kèm số lượng (món tạm hết hiển thị nhãn "tạm hết"), tổng giá realtime, nút **"Thêm cả combo vào giỏ"**.
- `POST /Combo/AddToCart` `[Authorize]` (controller storefront `ComboController` ở `Controllers/`):
  - `ComboService.AddComboToCartAsync(sessionId, comboId)` duyệt từng `ComboItem`:
    - Món hợp lệ → gọi `ICartService.AddToCartAsync(sessionId, productId, quantity, variantId)` hiện có (tái sử dụng 100% logic giá/tồn kho/variant).
    - Món không hợp lệ (hết hàng, product inactive/deleted, variant inactive) → gom vào danh sách bỏ qua.
  - Redirect về `Cart/Index` kèm `TempData` tóm tắt: "Đã thêm 3/5 món. Bỏ qua: Dọc mùng (hết hàng), ...".

## 5. Luồng dữ liệu

```
Shop view → ComboController (storefront) → ComboService
    → IUnitOfWork (Combo, ComboItem, Product, ProductVariant)
    → ICartService.AddToCartAsync (tái sử dụng nguyên trạng)
Admin views → ComboController (Admin) → ComboService → IUnitOfWork
Giá hiển thị card → IProductPricingService (giá realtime theo PriceSchedule)
```

`ComboService` đặt ở `Services/ComboService.cs` + `Services/Interfaces/IComboService.cs`, đăng ký DI trong `Program.cs` theo pattern các service khác.

## 6. Xử lý lỗi / tình huống biên

- Combo không tồn tại hoặc `IsActive = false` → không hiển thị trên Shop; `POST /Combo/AddToCart` → `NotFound`.
- Một phần món không khả dụng → thêm phần còn lại, cảnh báo liệt kê món bị bỏ qua kèm lý do.
- Tất cả món không khả dụng → không thêm gì, redirect lại Shop với thông báo lỗi.
- Khách chưa đăng nhập → `[Authorize]` đẩy về login (giống `AddToCart` hiện tại).
- Admin xóa sản phẩm đang nằm trong combo → `ComboItem` vẫn giữ tham chiếu; storefront tự lọc và hiển thị "tạm hết", không vỡ giao diện.
- Quantity combo vượt tồn kho → để `CartService.AddToCartAsync` hiện có xử lý, không viết lại logic tồn kho.

## 7. Kiểm thử

- Project không có test project → không thêm unit test (giữ convention hiện tại).
- Xác minh bằng:
  1. `dotnet build` — 0 error.
  2. Test tay bằng trình duyệt (Playwright MCP sẵn có):
     - Admin tạo combo mới với 2-3 món → thấy trong Index.
     - Shop hiển thị section "Mua theo món", giá card = tổng giá realtime.
     - Bấm "Thêm cả combo" → giỏ hàng có đúng món/variant/số lượng.
     - Set 1 món hết hàng → thêm combo → giỏ chỉ nhận món còn lại, có cảnh báo.
     - Admin xóa combo → biến mất khỏi Shop.
- Kết quả đạt: toàn bộ checklist trên pass.
