# Product Detail Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Áp dụng giao diện Clean Marketplace đã duyệt vào `Views/Shop/Detail.cshtml`, đồng bộ style review và related products, đảm bảo responsive + không regressions.

**Architecture:** Giữ nguyên controller/service; chỉ thay đổi Razor view, CSS chung và partial review. Bố cục mới full-width, bỏ sidebar, dùng Bootstrap 5 grid + custom CSS theo palette đã định.

**Tech Stack:** ASP.NET Core MVC 8, Razor, Bootstrap 5, Font Awesome, vanilla CSS trong `wwwroot/css/style.css`.

## Global Constraints
- Không thay đổi `Product` model, `ShopController`, cart/checkout services, SignalR hub.
- Không thêm thư viện frontend mới (không Tailwind, không JS framework).
- Giữ nguyên action forms: `Cart/AddToCart`, `Checkout/BuyNow`.
- Giữ nguyên id/element cần thiết cho SignalR stock update: `stockStatus`, `btnBuyNow`, `btnAddToCart`, `quantity`.
- Palette: primary `#6BA300`, primary-dark `#5A8A00`, primary-soft `#F1F8E8`, sale `#D9381E`, surface-warm `#FDFCF8`, text-main `#1F241B`.
- Font `Be Vietnam Pro` đã có trong `_Layout.cshtml`.
- Related products hiển thị tối đa 4 sản phẩm từ `ViewBag.RelatedProducts`.

---

## File Structure
- `Views/Shop/Detail.cshtml` — rewrite toàn bộ markup và inline styles (file chính).
- `wwwroot/css/style.css` — bổ sung CSS variables + utility classes dùng chung cho detail page.
- `Views/Shared/_ProductReviews.cshtml` — tinh chỉnh markup review summary để đồng bộ tông màu.
- `Views/Shared/_ProductCard.cshtml` — có thể giữ nguyên; detail page sẽ render related products bằng inline card mới trong `Detail.cshtml` để tránh ảnh hưởng shop grid.

---

### Task 1: Add shared CSS for product detail page

**Files:**
- Modify: `wwwroot/css/style.css` (append to end)

**Interfaces:**
- Produces: CSS custom properties and utility classes consumed by `Views/Shop/Detail.cshtml` and `_ProductReviews.cshtml`.

- [ ] **Step 1: Append CSS variables and component styles**

Add the following block to the end of `wwwroot/css/style.css`:

```css
/* Product Detail Redesign */
:root {
  --pd-primary: #6BA300;
  --pd-primary-dark: #5A8A00;
  --pd-primary-soft: #F1F8E8;
  --pd-sale: #D9381E;
  --pd-sale-soft: #FEF2F0;
  --pd-text: #1F241B;
  --pd-text-muted: #5F6B55;
  --pd-text-light: #8A9680;
  --pd-surface: #FFFFFF;
  --pd-surface-warm: #FDFCF8;
  --pd-surface-muted: #F4F6F1;
  --pd-border: #E8EBE3;
}

.product-detail-wrap {
  background: var(--pd-surface-warm);
}

/* Breadcrumb */
.pd-breadcrumb {
  padding: 18px 0;
  font-size: 0.875rem;
  color: var(--pd-text-light);
}
.pd-breadcrumb a {
  color: var(--pd-text-muted);
  transition: color .2s ease;
}
.pd-breadcrumb a:hover { color: var(--pd-primary); }
.pd-breadcrumb .sep { margin: 0 10px; color: var(--pd-border); }

/* Gallery */
.pd-gallery-main {
  position: relative;
  background: var(--pd-surface);
  border-radius: 24px;
  border: 1px solid var(--pd-border);
  overflow: hidden;
  aspect-ratio: 1 / 1;
  display: flex;
  align-items: center;
  justify-content: center;
}
.pd-gallery-main img {
  width: 85%;
  height: 85%;
  object-fit: contain;
  transition: transform .4s ease;
}
.pd-gallery-main:hover img { transform: scale(1.04); }

.pd-sale-ribbon {
  position: absolute;
  top: 18px;
  left: 18px;
  background: var(--pd-sale);
  color: #fff;
  font-weight: 800;
  font-size: 0.875rem;
  padding: 8px 14px;
  border-radius: 999px;
  box-shadow: 0 4px 12px rgba(217, 56, 30, 0.25);
  z-index: 2;
}

.pd-gallery-thumbs {
  display: flex;
  gap: 12px;
  margin-top: 16px;
}
.pd-thumb {
  width: 78px;
  height: 78px;
  border-radius: 8px;
  border: 2px solid var(--pd-border);
  background: var(--pd-surface);
  cursor: pointer;
  overflow: hidden;
  transition: all .2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}
.pd-thumb img { width: 80%; height: 80%; object-fit: contain; }
.pd-thumb:hover,
.pd-thumb.active {
  border-color: var(--pd-primary);
  box-shadow: 0 0 0 3px var(--pd-primary-soft);
}

/* Product info */
.pd-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 14px;
  font-size: 0.875rem;
}
.pd-category-badge {
  background: var(--pd-primary-soft);
  color: var(--pd-primary-dark);
  padding: 4px 12px;
  border-radius: 999px;
  font-weight: 600;
}
.pd-rating-mini { display: inline-flex; align-items: center; gap: 6px; color: var(--pd-text-muted); }
.pd-rating-mini .stars { color: #F5A623; }

.pd-title {
  font-size: clamp(1.75rem, 3vw, 2.5rem);
  font-weight: 800;
  line-height: 1.2;
  letter-spacing: -0.02em;
  margin-bottom: 18px;
  color: var(--pd-text);
}

.pd-trust-row {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 22px;
  margin-bottom: 24px;
  font-size: 0.875rem;
  color: var(--pd-text-muted);
}
.pd-trust-row span { display: inline-flex; align-items: center; gap: 8px; }
.pd-trust-row i { color: var(--pd-primary); font-size: 1rem; }

.pd-price-block {
  background: var(--pd-surface);
  border: 1px solid var(--pd-border);
  border-radius: 16px;
  padding: 20px 22px;
  margin-bottom: 22px;
}
.pd-price-main {
  display: flex;
  align-items: baseline;
  gap: 14px;
  flex-wrap: wrap;
  margin-bottom: 6px;
}
.pd-sale-price {
  font-size: 2rem;
  font-weight: 800;
  color: var(--pd-sale);
  letter-spacing: -0.02em;
}
.pd-original-price {
  font-size: 1.125rem;
  color: var(--pd-text-light);
  text-decoration: line-through;
}
.pd-discount-pill {
  background: var(--pd-sale-soft);
  color: var(--pd-sale);
  font-weight: 700;
  font-size: 0.8rem;
  padding: 4px 10px;
  border-radius: 999px;
}
.pd-unit-note {
  font-size: 0.9rem;
  color: var(--pd-text-muted);
  margin-top: 4px;
}

.pd-short-desc {
  color: var(--pd-text-muted);
  font-size: 1rem;
  max-width: 560px;
  margin-bottom: 22px;
}

.pd-spec-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
  margin-bottom: 24px;
}
.pd-spec-item {
  background: var(--pd-surface-muted);
  padding: 12px 14px;
  border-radius: 8px;
  font-size: 0.875rem;
}
.pd-spec-item .label { color: var(--pd-text-light); display: block; margin-bottom: 2px; }
.pd-spec-item .value { color: var(--pd-text); font-weight: 600; }

/* Actions */
.pd-action-row {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  align-items: center;
  margin-bottom: 16px;
}
.pd-qty {
  display: inline-flex;
  align-items: center;
  border: 1px solid var(--pd-border);
  border-radius: 999px;
  background: var(--pd-surface);
  overflow: hidden;
}
.pd-qty button {
  width: 42px;
  height: 42px;
  border: none;
  background: transparent;
  color: var(--pd-text);
  font-size: 0.85rem;
  transition: background .2s ease;
}
.pd-qty button:hover { background: var(--pd-surface-muted); }
.pd-qty button:active { transform: scale(0.95); }
.pd-qty input {
  width: 46px;
  border: none;
  text-align: center;
  font-weight: 700;
  color: var(--pd-text);
  background: transparent;
}

.pd-btn-buy {
  flex: 1 1 160px;
  background: var(--pd-sale);
  color: #fff;
  border: none;
  padding: 14px 28px;
  border-radius: 999px;
  font-weight: 700;
  font-size: 1rem;
  transition: all .2s ease;
  box-shadow: 0 6px 18px rgba(217, 56, 30, 0.22);
}
.pd-btn-buy:hover { background: #C22F18; transform: translateY(-2px); box-shadow: 0 10px 24px rgba(217, 56, 30, 0.28); }
.pd-btn-buy:active { transform: translateY(0) scale(0.98); }

.pd-btn-cart {
  flex: 1 1 160px;
  background: var(--pd-surface);
  color: var(--pd-primary);
  border: 2px solid var(--pd-primary);
  padding: 13px 26px;
  border-radius: 999px;
  font-weight: 700;
  font-size: 1rem;
  transition: all .2s ease;
}
.pd-btn-cart:hover { background: var(--pd-primary); color: #fff; }
.pd-btn-cart:active { transform: scale(0.98); }

.pd-btn-wish {
  width: 50px;
  height: 50px;
  border-radius: 50%;
  border: 1px solid var(--pd-border);
  background: var(--pd-surface);
  color: var(--pd-text-muted);
  transition: all .2s ease;
}
.pd-btn-wish:hover { border-color: var(--pd-sale); color: var(--pd-sale); }

.pd-stock {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--pd-primary-dark);
  background: var(--pd-primary-soft);
  padding: 8px 14px;
  border-radius: 999px;
}

/* Tabs */
.pd-tabs { margin-top: 64px; }
.pd-nav-tabs {
  border-bottom: 2px solid var(--pd-border);
  gap: 8px;
}
.pd-nav-tabs .nav-link {
  border: none;
  background: transparent;
  color: var(--pd-text-muted);
  font-weight: 600;
  padding: 14px 20px;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: all .2s ease;
}
.pd-nav-tabs .nav-link.active {
  color: var(--pd-primary-dark);
  border-bottom-color: var(--pd-primary);
}
.pd-tab-content { padding: 32px 0; }
.pd-description {
  max-width: 760px;
  color: var(--pd-text-muted);
  font-size: 1rem;
  line-height: 1.8;
}

/* Review summary */
.pd-review-summary {
  display: grid;
  grid-template-columns: 220px 1fr;
  gap: 40px;
  background: var(--pd-surface);
  border: 1px solid var(--pd-border);
  border-radius: 16px;
  padding: 28px;
  margin-bottom: 28px;
}
.pd-review-average { text-align: center; border-right: 1px solid var(--pd-border); }
.pd-review-average .big {
  font-size: 3.5rem;
  font-weight: 800;
  line-height: 1;
  color: var(--pd-text);
}
.pd-review-average .stars { color: #F5A623; font-size: 1.1rem; margin: 8px 0; }
.pd-review-bars .bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 10px;
  font-size: 0.875rem;
}
.pd-review-bars .bar span { width: 60px; color: var(--pd-text-muted); }
.pd-review-bars .progress-line {
  flex: 1;
  height: 8px;
  background: var(--pd-surface-muted);
  border-radius: 999px;
  overflow: hidden;
}
.pd-review-bars .progress-fill {
  height: 100%;
  background: #F5A623;
  border-radius: 999px;
}

/* Related products */
.pd-section-title {
  font-size: 1.5rem;
  font-weight: 800;
  margin-bottom: 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.pd-section-title a { font-size: 0.9rem; font-weight: 600; color: var(--pd-primary); }

.pd-related-card {
  background: var(--pd-surface);
  border: 1px solid var(--pd-border);
  border-radius: 16px;
  overflow: hidden;
  transition: all .2s ease;
  height: 100%;
  display: flex;
  flex-direction: column;
}
.pd-related-card:hover {
  transform: translateY(-6px);
  box-shadow: 0 18px 48px rgba(31, 36, 27, 0.12);
  border-color: transparent;
}
.pd-related-img {
  position: relative;
  aspect-ratio: 4 / 3;
  background: var(--pd-surface-warm);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}
.pd-related-img img { width: 78%; height: 78%; object-fit: contain; transition: transform .35s ease; }
.pd-related-card:hover .pd-related-img img { transform: scale(1.05); }
.pd-related-badge {
  position: absolute;
  top: 12px;
  left: 12px;
  background: var(--pd-sale);
  color: #fff;
  font-size: 0.75rem;
  font-weight: 700;
  padding: 4px 10px;
  border-radius: 999px;
}
.pd-related-body { padding: 18px; display: flex; flex-direction: column; flex: 1; }
.pd-related-title {
  font-size: 1rem;
  font-weight: 700;
  margin-bottom: 8px;
  line-height: 1.4;
}
.pd-related-desc {
  font-size: 0.875rem;
  color: var(--pd-text-muted);
  margin-bottom: 14px;
  line-height: 1.5;
  flex: 1;
}
.pd-related-bottom {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}
.pd-related-price { font-weight: 800; color: var(--pd-sale); font-size: 1.05rem; }
.pd-related-price .unit { color: var(--pd-text-light); font-size: 0.8rem; font-weight: 500; }
.pd-related-add {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  border: 1px solid var(--pd-border);
  background: var(--pd-surface);
  color: var(--pd-primary);
  transition: all .2s ease;
}
.pd-related-add:hover { background: var(--pd-primary); color: #fff; border-color: var(--pd-primary); }

/* Mobile sticky CTA */
.pd-mobile-cta {
  display: none;
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  background: var(--pd-surface);
  border-top: 1px solid var(--pd-border);
  padding: 12px 16px;
  gap: 12px;
  z-index: 1030;
  box-shadow: 0 -4px 20px rgba(31, 36, 27, 0.06);
}

@media (max-width: 991px) {
  .pd-desktop-cta { display: none !important; }
  .pd-mobile-cta { display: flex; }
  .product-detail-wrap { padding-bottom: 80px; }
}

@media (max-width: 767px) {
  .pd-spec-grid { grid-template-columns: 1fr; }
  .pd-gallery-thumbs { gap: 8px; }
  .pd-thumb { width: 60px; height: 60px; }
  .pd-tabs { margin-top: 40px; }
  .pd-review-summary { grid-template-columns: 1fr; gap: 24px; }
  .pd-review-average { border-right: none; border-bottom: 1px solid var(--pd-border); padding-bottom: 20px; }
}
```

- [ ] **Step 2: Build project to check CSS syntax**

Run:
```bash
dotnet build Fruitables.csproj --no-restore
```

Expected: Build succeeds (CSS file is static, no compile error).

- [ ] **Step 3: Commit**

```bash
git add wwwroot/css/style.css
git commit -m "style(detail): add shared CSS for product detail redesign"
```

---

### Task 2: Rewrite `Views/Shop/Detail.cshtml`

**Files:**
- Modify: `Views/Shop/Detail.cshtml`

**Interfaces:**
- Consumes: `Product` model properties (`Id`, `Name`, `Slug`, `Description`, `ShortDescription`, `Price`, `SalePrice`, `Unit`, `Weight`, `StockQuantity`, `MinOrderQuantity`, `CountryOrigin`, `Quality`, `AverageRating`, `ReviewCount`, `Category`, `Images`).
- Consumes: `ViewBag.RelatedProducts` as `List<Product>`.
- Produces: rendered HTML with forms `addToCartForm` and `buyNowForm`, elements `quantity`, `hiddenQuantity`, `hiddenQuantityBuyNow`, `stockStatus`, `btnBuyNow`, `btnAddToCart`.

- [ ] **Step 1: Replace full content of `Views/Shop/Detail.cshtml`**

Use the following Razor markup (preserve existing model and variable declarations):

```html
@using Fruitables.Models
@model Product
@{
    ViewData["Title"] = Model.Name;
    var primaryImage = Model.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
        ?? Model.Images?.FirstOrDefault()?.ImageUrl
        ?? "~/img/single-item.jpg";
    var relatedProducts = ViewBag.RelatedProducts as List<Product> ?? new List<Product>();
    var isSale = Model.SalePrice.HasValue && Model.SalePrice < Model.Price;
    var discountPercent = isSale ? (int)Math.Round((1 - (Model.SalePrice!.Value / Model.Price)) * 100) : 0;
    var avgRating = (double)Model.AverageRating;
    var reviewCount = Model.ReviewCount;
}

<div class="product-detail-wrap">
    <div class="container">
        <!-- Breadcrumb -->
        <nav aria-label="breadcrumb" class="pd-breadcrumb">
            <a asp-controller="Home" asp-action="Index">Trang chủ</a>
            <span class="sep">/</span>
            <a asp-controller="Shop" asp-action="Index">Cửa hàng</a>
            <span class="sep">/</span>
            @if (Model.Category != null)
            {
                <a asp-controller="Shop" asp-action="Index" asp-route-categoryId="@Model.Category.Id">@Model.Category.Name</a>
                <span class="sep">/</span>
            }
            <span style="color:var(--pd-text); font-weight:500;">@Model.Name</span>
        </nav>

        <!-- Product hero -->
        <div class="row g-5 align-items-start">
            <!-- Gallery -->
            <div class="col-lg-6">
                <div class="pd-gallery-main">
                    @if (isSale)
                    {
                        <span class="pd-sale-ribbon">-@discountPercent%</span>
                    }
                    <img id="mainImage" src="@primaryImage" alt="@Model.Name">
                </div>
                @if (Model.Images?.Count > 1)
                {
                    <div class="pd-gallery-thumbs">
                        @foreach (var image in Model.Images.OrderBy(i => i.SortOrder).Take(5))
                        {
                            var isActive = image.ImageUrl == primaryImage ? "active" : "";
                            <div class="pd-thumb @isActive" onclick="changeImage(this, '@image.ImageUrl')">
                                <img src="@image.ImageUrl" alt="@Model.Name">
                            </div>
                        }
                    </div>
                }
            </div>

            <!-- Info -->
            <div class="col-lg-6">
                <div class="pd-meta">
                    @if (Model.Category != null)
                    {
                        <a asp-controller="Shop" asp-action="Index" asp-route-categoryId="@Model.Category.Id" class="pd-category-badge">@Model.Category.Name</a>
                    }
                    @if (reviewCount > 0)
                    {
                        <span class="pd-rating-mini">
                            <span class="stars">
                                @for (int i = 0; i < 5; i++)
                                {
                                    <i class="fa fa-star @(i < avgRating ? "" : "text-muted")"></i>
                                }
                            </span>
                            <strong>@avgRating.ToString("F1")</strong>
                            <span>(@reviewCount đánh giá)</span>
                        </span>
                    }
                </div>

                <h1 class="pd-title">@Model.Name</h1>

                <div class="pd-trust-row">
                    <span><i class="fa fa-truck-fast"></i> Giao nhanh 2 giờ</span>
                    <span><i class="fa fa-seedling"></i> Tươi mỗi ngày</span>
                    <span><i class="fa fa-rotate-left"></i> Đổi trả trong 24 giờ</span>
                </div>

                <div class="pd-price-block">
                    <div class="pd-price-main">
                        @if (isSale)
                        {
                            <span class="pd-sale-price">@Model.SalePrice!.Value.ToString("N0")đ</span>
                            <span class="pd-original-price">@Model.Price.ToString("N0")đ</span>
                            <span class="pd-discount-pill">Tiết kiệm @((Model.Price - Model.SalePrice.Value).ToString("N0"))đ</span>
                        }
                        else
                        {
                            <span class="pd-sale-price" style="color:var(--pd-text);">@Model.Price.ToString("N0")đ</span>
                        }
                    </div>
                    <div class="pd-unit-note">/ @Model.Unit · Giá đã bao gồm thuế VAT</div>
                </div>

                @if (!string.IsNullOrEmpty(Model.ShortDescription))
                {
                    <p class="pd-short-desc">@Model.ShortDescription</p>
                }

                <div class="pd-spec-grid">
                    @if (!string.IsNullOrEmpty(Model.CountryOrigin))
                    {
                        <div class="pd-spec-item">
                            <span class="label">Xuất xứ</span>
                            <span class="value">@Model.CountryOrigin</span>
                        </div>
                    }
                    @if (!string.IsNullOrEmpty(Model.Quality))
                    {
                        <div class="pd-spec-item">
                            <span class="label">Chất lượng</span>
                            <span class="value">@Model.Quality</span>
                        </div>
                    }
                    @if (Model.Weight.HasValue)
                    {
                        <div class="pd-spec-item">
                            <span class="label">Khối lượng</span>
                            <span class="value">@Model.Weight.Value.ToString("N0")g / gói</span>
                        </div>
                    }
                    <div class="pd-spec-item">
                        <span class="label">Tồn kho</span>
                        <span class="value">Còn @Model.StockQuantity @Model.Unit</span>
                    </div>
                </div>

                <form asp-controller="Cart" asp-action="AddToCart" method="post" class="d-inline" id="addToCartForm">
                    <input type="hidden" name="productId" value="@Model.Id" />
                    <input type="hidden" name="quantity" id="hiddenQuantity" value="@Model.MinOrderQuantity" />
                </form>
                <form asp-controller="Checkout" asp-action="BuyNow" method="post" class="d-inline" id="buyNowForm">
                    <input type="hidden" name="productId" value="@Model.Id" />
                    <input type="hidden" name="quantity" id="hiddenQuantityBuyNow" value="@Model.MinOrderQuantity" />
                </form>

                <div class="pd-desktop-cta">
                    <div class="pd-action-row">
                        <div class="pd-qty">
                            <button type="button" onclick="decreaseQty()"><i class="fa fa-minus"></i></button>
                            <input type="text" id="quantity" value="@Model.MinOrderQuantity" min="@Model.MinOrderQuantity" readonly>
                            <button type="button" onclick="increaseQty()"><i class="fa fa-plus"></i></button>
                        </div>
                        <button type="submit" form="addToCartForm" class="pd-btn-cart" id="btnAddToCart" @(Model.StockQuantity <= 0 ? "disabled" : "")>
                            <i class="fa fa-shopping-bag me-2"></i>Thêm vào giỏ
                        </button>
                        <button type="submit" form="buyNowForm" class="pd-btn-buy" id="btnBuyNow" @(Model.StockQuantity <= 0 ? "disabled" : "")>
                            <i class="fa fa-bolt me-2"></i>Mua ngay
                        </button>
                        <button type="button" class="pd-btn-wishlist" title="Yêu thích"><i class="fa fa-heart"></i></button>
                    </div>
                    <div class="pd-stock" id="stockStatus">
                        @if (Model.StockQuantity > 0)
                        {
                            <i class="fa fa-check-circle"></i>
                            <span>Còn hàng — giao hôm nay nếu đặt trước 16:00</span>
                        }
                        else
                        {
                            <i class="fa fa-times-circle"></i>
                            <span>Hết hàng</span>
                        }
                    </div>
                </div>
            </div>
        </div>

        <!-- Tabs -->
        <div class="pd-tabs">
            <ul class="nav pd-nav-tabs" id="pdTab" role="tablist">
                <li class="nav-item" role="presentation">
                    <button class="nav-link active" id="pd-desc-tab" data-bs-toggle="tab" data-bs-target="#pd-desc" type="button" role="tab">Mô tả sản phẩm</button>
                </li>
                <li class="nav-item" role="presentation">
                    <button class="nav-link" id="pd-review-tab" data-bs-toggle="tab" data-bs-target="#pd-review" type="button" role="tab">Đánh giá (@reviewCount)</button>
                </li>
            </ul>
            <div class="tab-content pd-tab-content" id="pdTabContent">
                <div class="tab-pane fade show active" id="pd-desc" role="tabpanel">
                    <div class="pd-description">
                        @Html.Raw(Model.Description?.Replace("\n", "<br />") ?? "<p>Chưa có mô tả chi tiết.</p>")
                    </div>
                </div>
                <div class="tab-pane fade" id="pd-review" role="tabpanel">
                    <partial name="_ProductReviews" model="Model.Id" />
                </div>
            </div>
        </div>

        <!-- Related products -->
        @if (relatedProducts.Any())
        {
            <div class="mt-5 pt-4">
                <div class="pd-section-title">
                    <span>Sản phẩm liên quan</span>
                    <a asp-controller="Shop" asp-action="Index">Xem tất cả <i class="fa fa-arrow-right ms-1"></i></a>
                </div>
                <div class="row g-4">
                    @foreach (var product in relatedProducts.Take(4))
                    {
                        var rpImage = product.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                            ?? product.Images?.FirstOrDefault()?.ImageUrl
                            ?? "~/img/fruite-item-5.jpg";
                        var rpSale = product.SalePrice.HasValue && product.SalePrice < product.Price;
                        var rpDiscount = rpSale ? (int)Math.Round((1 - (product.SalePrice!.Value / product.Price)) * 100) : 0;
                        var rpPrice = rpSale ? product.SalePrice!.Value : product.Price;

                        <div class="col-6 col-md-4 col-lg-3">
                            <div class="pd-related-card">
                                <a asp-controller="Shop" asp-action="Detail" asp-route-slug="@product.Slug" class="pd-related-img">
                                    @if (rpSale)
                                    {
                                        <span class="pd-related-badge">-@rpDiscount%</span>
                                    }
                                    <img src="@rpImage" alt="@product.Name">
                                </a>
                                <div class="pd-related-body">
                                    <a asp-controller="Shop" asp-action="Detail" asp-route-slug="@product.Slug" class="pd-related-title">@product.Name</a>
                                    <p class="pd-related-desc">
                                        @(product.ShortDescription ?? product.Description?.Substring(0, Math.Min(product.Description?.Length ?? 0, 60)) ?? "Tươi ngon, phù hợp cho bữa ăn gia đình.")
                                    </p>
                                    <div class="pd-related-bottom">
                                        <div class="pd-related-price">
                                            @rpPrice.ToString("N0")đ <span class="unit">/ @product.Unit</span>
                                        </div>
                                        <form asp-controller="Cart" asp-action="AddToCart" method="post">
                                            <input type="hidden" name="productId" value="@product.Id" />
                                            <input type="hidden" name="quantity" value="@product.MinOrderQuantity" />
                                            <button type="submit" class="pd-related-add" @(product.StockQuantity <= 0 ? "disabled" : "")><i class="fa fa-plus"></i></button>
                                        </form>
                                    </div>
                                </div>
                            </div>
                        </div>
                    }
                </div>
            </div>
        }
    </div>
</div>

<!-- Mobile sticky CTA -->
<div class="pd-mobile-cta">
    <button type="submit" form="addToCartForm" class="pd-btn-cart" id="btnAddToCartMobile" @(Model.StockQuantity <= 0 ? "disabled" : "")>
        <i class="fa fa-shopping-bag me-2"></i>Thêm vào giỏ
    </button>
    <button type="submit" form="buyNowForm" class="pd-btn-buy" id="btnBuyNowMobile" @(Model.StockQuantity <= 0 ? "disabled" : "")>
        <i class="fa fa-bolt me-2"></i>Mua ngay
    </button>
</div>

@section Scripts {
    <script src="~/js/review.js" asp-append-version="true"></script>
    <script>
        const minQty = @Model.MinOrderQuantity;
        let maxQty = @Model.StockQuantity;

        function updateQuantity(newVal) {
            document.getElementById('quantity').value = newVal;
            document.getElementById('hiddenQuantity').value = newVal;
            document.getElementById('hiddenQuantityBuyNow').value = newVal;
        }

        function increaseQty() {
            var qty = document.getElementById('quantity');
            var currentVal = parseInt(qty.value);
            if (currentVal < maxQty) {
                updateQuantity(currentVal + 1);
            }
        }

        function decreaseQty() {
            var qty = document.getElementById('quantity');
            var currentVal = parseInt(qty.value);
            if (currentVal > minQty) {
                updateQuantity(currentVal - 1);
            }
        }

        function changeImage(el, src) {
            document.getElementById('mainImage').src = src;
            document.querySelectorAll('.pd-thumb').forEach(t => t.classList.remove('active'));
            el.classList.add('active');
        }

        if (window.ecommerceHub) {
            const currentProductId = @Model.Id;

            window.ecommerceHub.on("StockChanged", function(data) {
                if (data.productId === currentProductId) {
                    maxQty = data.stock;
                    var qtyInput = document.getElementById('quantity');
                    var currentVal = parseInt(qtyInput.value);
                    if (currentVal > maxQty && maxQty > 0) {
                        updateQuantity(maxQty);
                    }

                    var stockStatus = document.getElementById('stockStatus');
                    var btns = [
                        document.getElementById('btnBuyNow'),
                        document.getElementById('btnAddToCart'),
                        document.getElementById('btnBuyNowMobile'),
                        document.getElementById('btnAddToCartMobile')
                    ];

                    if (maxQty > 0) {
                        stockStatus.innerHTML = '<i class="fa fa-check-circle"></i><span>Còn hàng — giao hôm nay nếu đặt trước 16:00</span>';
                        btns.forEach(b => b && b.removeAttribute('disabled'));
                    } else {
                        stockStatus.innerHTML = '<i class="fa fa-times-circle"></i><span>Hết hàng</span>';
                        btns.forEach(b => b && b.setAttribute('disabled', 'disabled'));
                    }

                    if (typeof showRealtimeToast === 'function') {
                        showRealtimeToast('Số lượng tồn kho vừa được cập nhật tự động!', false);
                    }
                }
            });

            window.ecommerceHubReady.then(() => {
                return window.ecommerceHub.invoke("JoinProductGroup", currentProductId);
            }).catch(err => console.error(err));
        }
    </script>
}
```

- [ ] **Step 2: Build project**

Run:
```bash
dotnet build Fruitables.csproj --no-restore
```

Expected: Build succeeds.

- [ ] **Step 3: Run existing tests**

Run:
```bash
dotnet test Tests/Fruitables.Tests.csproj --no-build
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add Views/Shop/Detail.cshtml
git commit -m "feat(shop): redesign product detail page layout"
```

---

### Task 3: Sync review partial styling

**Files:**
- Modify: `Views/Shared/_ProductReviews.cshtml`

**Interfaces:**
- Consumes: `ViewBag.ReviewStatistics` and `ViewBag.Reviews`.
- Produces: HTML with classes matching `style.css` (`pd-review-summary`, `pd-review-average`, etc.).

- [ ] **Step 1: Update review statistics markup**

Replace the existing review-statistics `div` (lines ~17-51) with:

```html
@if (statistics != null && statistics.TotalReviews > 0)
{
    <div class="pd-review-summary">
        <div class="pd-review-average">
            <div class="big">@statistics.AverageRating.ToString("F1")</div>
            <div class="stars">
                @for (int i = 0; i < 5; i++)
                {
                    <i class="fa fa-star @(i < Math.Round(statistics.AverageRating) ? "" : "text-muted")"></i>
                }
            </div>
            <p class="text-muted mb-0">@statistics.TotalReviews đánh giá</p>
        </div>
        <div class="pd-review-bars">
            <h6 class="mb-3">Phân bố đánh giá</h6>
            @for (int star = 5; star >= 1; star--)
            {
                var count = star == 5 ? statistics.FiveStarCount :
                            star == 4 ? statistics.FourStarCount :
                            star == 3 ? statistics.ThreeStarCount :
                            star == 2 ? statistics.TwoStarCount :
                            statistics.OneStarCount;
                var percentage = statistics.TotalReviews > 0 ? (count * 100.0 / statistics.TotalReviews) : 0;

                <div class="bar">
                    <span>@star <i class="fa fa-star text-warning"></i></span>
                    <div class="progress-line">
                        <div class="progress-fill" style="width: @percentage%"></div>
                    </div>
                    <span class="text-muted" style="width: 50px;">@count</span>
                </div>
            }
        </div>
    </div>
}
```

- [ ] **Step 2: Build and test**

Run:
```bash
dotnet build Fruitables.csproj --no-restore
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Views/Shared/_ProductReviews.cshtml
git commit -m "feat(reviews): sync review summary styling with detail redesign"
```

---

### Task 4: Verification & polish

**Files:**
- No file changes; visual/manual verification.

- [ ] **Step 1: Start app and open product detail**

Ensure no running Fruitables.exe locks build artifacts:
```bash
tasklist | grep -i Fruitables.exe && taskkill //F //IM Fruitables.exe
```

Run:
```bash
dotnet run --project Fruitables.csproj
```

Open browser at `https://localhost:7156/Shop/Detail/{slug}` (or the port in `launchSettings.json`).

- [ ] **Step 2: Visual checklist**

Verify:
- [ ] Breadcrumb đơn giản, không có background image cũ.
- [ ] Gallery hiển thị ảnh chính + thumbnails, click thumbnail đổi ảnh.
- [ ] Tên sản phẩm lớn, category badge, rating hiển thị đúng.
- [ ] Trust badges xuất hiện.
- [ ] Giá sale/giá gốc/discount pill đúng.
- [ ] Spec grid hiển thị xuất xứ, chất lượng, khối lượng, tồn kho.
- [ ] Nút “Thêm vào giỏ” và “Mua ngay” hoạt động (thử tăng quantity).
- [ ] Tab Mô tả / Đánh giá hoạt động.
- [ ] Review summary có big score + progress bars.
- [ ] Related products hiển thị 4 sản phẩm, hover mượt.
- [ ] Mobile: CTA sticky ở bottom, bố cục 1 cột.

- [ ] **Step 3: Regression tests**

Run:
```bash
dotnet test Tests/Fruitables.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 4: Commit verification results / final polish**

If any minor CSS tweaks needed, edit `wwwroot/css/style.css` or `Views/Shop/Detail.cshtml`, then commit:

```bash
git add .
git commit -m "polish(detail): responsive and visual tweaks after verification"
```

---

## Spec Coverage Check

| Spec requirement | Task |
|------------------|------|
| Palette & typography | Task 1 CSS variables + Task 2 markup classes |
| Full-width layout, bỏ sidebar | Task 2 markup |
| Gallery large + thumbnails | Task 2 gallery section |
| Trust badges | Task 2 info section |
| Price block nổi bật | Task 2 price block |
| Spec grid | Task 2 spec grid |
| CTA buttons + quantity | Task 2 action row + mobile CTA |
| Sticky mobile CTA | Task 2 mobile bar |
| Tabs Mô tả / Đánh giá | Task 2 tabs + Task 3 review styling |
| Review summary big score + bars | Task 3 |
| Related products card mới | Task 2 related section |
| Responsive | Task 1 media queries + Task 4 verification |
| Giữ nguyên controller/service/model | Plan constraints + Task 2 form IDs |
| SignalR stock update | Task 2 JS giữ id elements |

## Placeholder Scan
- No TBD/TODO.
- No vague instructions like "add appropriate error handling".
- All code blocks contain concrete markup/CSS.
- All file paths exact.

## Type Consistency
- Form IDs: `addToCartForm`, `buyNowForm`, `hiddenQuantity`, `hiddenQuantityBuyNow`, `quantity`, `stockStatus`, `btnBuyNow`, `btnAddToCart` — giữ nguyên và mở rộng mobile variants.
- Model properties match `Product` model đã đọc.
- `ViewBag.RelatedProducts` typed as `List<Product>`.
