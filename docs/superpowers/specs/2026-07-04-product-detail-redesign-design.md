# Product Detail Redesign — Nâng cấp giao diện trang chi tiết sản phẩm

## Tóm tắt
Nâng cấp giao diện `Views/Shop/Detail.cshtml` theo hướng **Clean Marketplace**, chuẩn thương mại điện tử: bố cục full-width, bỏ sidebar, gallery lớn hơn, thông tin sản phẩm phân cấp rõ ràng, CTA nổi bật, sticky mobile CTA, trust badges, review summary và related products dạng card hiện đại.

Mockup tham khảo: `docs/mockups/product-detail-redesign.html`.

## Phạm vi
- **Chỉ redesign UI/UX layout & visual** — không thay đổi model `Product`, `ProductVariant`, `ProductImage`.
- **Không thêm tính năng nghiệp vụ mới** — giữ nguyên logic giỏ hàng, mua ngay, đánh giá, SignalR tồn kho.
- **Bỏ sidebar** (`_ShopSidebar`) trên trang chi tiết để tập trung vào sản phẩm.

## Ngữ cảnh hiện tại
- `Controllers/ShopController.cs`: action `Detail(string slug)` trả về view `Detail` với model `Product`, cùng các `ViewBag`: `RelatedProducts`, `CategoryTree`, `FeaturedProducts`, `ReviewStatistics`, `Reviews`, `CanReview`, `CartCount`.
- `Views/Shop/Detail.cshtml`: layout 8 cột nội dung + 4 cột sidebar, style inline cơ bản.
- `_ProductReviews.cshtml`: render review statistics, list review, modal viết đánh giá.
- `_ProductCard.cshtml`: card sản phẩm dùng chung cho grid related products.
- Style chính: `wwwroot/css/style.css` (Bootstrap 5 + custom).

## Thiết kế

### Palette & typography
- **Primary**: `#6BA300` (xanh lá đậm hơn, bớt oversaturated so với `#81c408` hiện tại).
- **Primary dark**: `#5A8A00`.
- **Primary soft**: `#F1F8E8`.
- **Sale/accent**: `#D9381E`.
- **Sale soft**: `#FEF2F0`.
- **Text main**: `#1F241B`.
- **Text muted**: `#5F6B55`.
- **Text light**: `#8A9680`.
- **Surface**: `#FFFFFF`.
- **Surface warm**: `#FDFCF8` (nền trang).
- **Surface muted**: `#F4F6F1`.
- **Border**: `#E8EBE3`.
- Font giữ `Be Vietnam Pro` (đã có) nhưng tinh chỉnh: heading lớn hơn, letter-spacing âm nhẹ, dùng thêm weight 500/600/800.

### Bố cục tổng thể
1. **Breadcrumb** — đơn giản hơn, không dùng background image, text-only trên nền warm white.
2. **Product hero** — 2 cột (`col-lg-6`):
   - **Trái**: Gallery ảnh sản phẩm.
   - **Phải**: Thông tin + CTA.
3. **Tab section** — Mô tả / Đánh giá, full-width.
4. **Related products** — grid 4 cột, card mới.

### Gallery
- Ảnh chính: container vuông/clean, border radius lớn (`24px`), border nhẹ, background trắng.
- Badge sale dạng pill màu đỏ, absolute top-left.
- Thumbnail row dưới ảnh chính: 4-5 thumb, border radius `8px`, border `2px`, active state với ring xanh lá.
- Hover ảnh chính: scale nhẹ (`1.04`).

### Product info
- **Category badge**: pill xanh lá soft.
- **Rating mini**: sao vàng + số điểm + số lượt đánh giá.
- **Product title**: lớn (`clamp(1.75rem, 3vw, 2.5rem)`), weight 800, letter-spacing `-0.02em`.
- **Trust badges row**: 3 mục — Giao nhanh 2 giờ / Tươi mỗi ngày / Đổi trả trong 24 giờ.
- **Price block**: khối nổi bật với border, border radius `16px`:
  - Sale price lớn, màu đỏ, weight 800.
  - Original price gạch ngang, màu xám nhạt.
  - Discount pill (tiết kiệm bao nhiêu).
  - Đơn vị + ghi chú VAT bên dưới.
- **Short description**: đoạn mô tả ngắn, max-width `560px`.
- **Spec grid**: 2 cột trên desktop, 1 cột mobile. Các mục: Xuất xứ, Chất lượng, Khối lượng, Tồn kho.
- **Actions**:
  - Quantity control dạng pill: `-` / input / `+`.
  - Nút “Thêm vào giỏ” — viền xanh lá, hover fill xanh.
  - Nút “Mua ngay” — màu đỏ sale, shadow tinted, hover translateY + shadow đậm.
  - Nút yêu thích — circle outline, hover đỏ.
- **Stock status**: pill xanh lá soft, thông báo “Còn hàng — giao hôm nay nếu đặt trước 16:00”.

### Mobile CTA sticky
- Trên màn hình `< 992px`, ẩn desktop CTA.
- Hiển thị fixed bar ở bottom với 2 nút: “Thêm vào giỏ” + “Mua ngay”.
- Body padding-bottom để tránh bị che nội dung.

### Tabs
- Nav tabs dạng underline, không dùng Bootstrap default.
- Active: text xanh lá đậm + border-bottom xanh lá.
- Nội dung tab:
  - **Mô tả**: typography dễ đọc, line-height `1.8`, giữ định dạng xuống dòng từ `Model.Description`.
  - **Đánh giá**: review summary (big score + 5 progress bars) + list review + dropdown sort.

### Related products
- Card mới: border nhẹ, border radius `16px`, background trắng.
- Image wrap: tỷ lệ `4/3`, background warm, object-fit contain.
- Hover: translateY `-6px`, shadow lớn hơn, border transparent.
- Price màu đỏ sale, unit xám.
- Nút “+” tròn để thêm nhanh.

## Data flow
Giữ nguyên controller và service. Chỉ thay đổi cách render:

```
GET /Shop/Detail/{slug}
  → ShopController.Detail()
  → View("Detail", product)
  → Render Views/Shop/Detail.cshtml mới
```

Các form `AddToCart` và `BuyNow` giữ nguyên action, chỉ cải thiện CSS/JS quantity.
SignalR cập nhật tồn kho real-time vẫn hoạt động với các element id cũ/mới tương thích.

## Files cần chỉnh sửa
- `Views/Shop/Detail.cshtml` — rewrite markup và style.
- `wwwroot/css/style.css` — bổ sung utility classes chung nếu cần (ví dụ: `.product-title`, `.btn-buy-now`, `.gallery-thumb`).
- Có thể cần tinh chỉnh nhẹ `Views/Shared/_ProductReviews.cshtml` để đồng bộ style review summary.
- Không bắt buộc: cập nhật `_ProductCard.cshtml` nếu muốn related products dùng card mới (có thể làm riêng partial hoặc inline trong `Detail.cshtml`).

## Dependencies
- Bootstrap 5 (đã có trong `_Layout.cshtml`).
- Font Awesome (đã có).
- Google Fonts `Be Vietnam Pro` (đã có).
- Không thêm thư viện mới.

## Không thay đổi
- `ShopController.Detail()` và các service liên quan.
- `Product` model và các navigation properties.
- Logic đánh giá (`_ProductReviews.cshtml` JS, `review.js`).
- SignalR hub / real-time stock update.
- Form POST actions (`Cart/AddToCart`, `Checkout/BuyNow`).

## Rủi ro & giảm thiểu
| Rủi ro | Giảm thiểu |
|--------|------------|
| Layout bị vỡ trên mobile khi bỏ sidebar | Test responsive kỹ, dùng Bootstrap grid `col-lg-6`, sticky CTA mobile |
| CTA forms cũ bị ảnh hưởng khi thay markup | Giữ nguyên `form` action, input name, và `id` quantity |
| SignalR stock update không tìm đúng element | Giữ/gán lại id `stockStatus`, `btnBuyNow`, `btnAddToCart`, `quantity` |
| Màu sắc mới không đồng nhất với các trang cũ | Dùng palette gần với màu hiện tại, chỉ tinh chỉnh saturation/shade |
| `_ProductReviews` style cũ lệch tông | Tinh chỉnh CSS review summary trong view chính hoặc partial |

## Acceptance Criteria
- [ ] Trang `Shop/Detail` hiển thị bố cục full-width mới, không còn sidebar bên phải.
- [ ] Gallery có ảnh chính + thumbnails, active state rõ ràng, hover zoom nhẹ.
- [ ] Hiển thị đúng tên, danh mục, giá sale/giá gốc, đơn vị, mô tả ngắn, xuất xứ, chất lượng, tồn kho.
- [ ] Có trust badges (giao nhanh, tươi mỗi ngày, đổi trả) — có thể hard-code text.
- [ ] CTA “Mua ngay” và “Thêm vào giỏ” hoạt động đúng, quantity cập nhật đúng form hidden.
- [ ] Trạng thái tồn kho cập nhật real-time qua SignalR.
- [ ] Sticky mobile CTA hiển thị trên màn hình nhỏ.
- [ ] Tab Mô tả / Đánh giá hoạt động, nội dung đọc dễ.
- [ ] Review summary hiển thị big score + distribution bars.
- [ ] Related products render grid 4 cột trên desktop, 2 cột mobile, hover mượt.
- [ ] Responsive tốt trên desktop, tablet, mobile.
