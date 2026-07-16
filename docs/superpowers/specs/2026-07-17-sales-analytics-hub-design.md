# Sales Analytics Hub — Design Spec

**Date:** 2026-07-17  
**Status:** Draft for user review  
**Stack:** ASP.NET Core MVC Admin, EF Core, Chart.js, ClosedXML  
**Prototype (throwaway):** `docs/superpowers/prototypes/sales-analytics-hub/index.html`

## 1. Problem

Admin currently has two separate statistics surfaces:

- `/Admin/Revenue` — revenue overview, trend, category, top products, Excel export  
- `/Admin/Revenue/CancelledOrders` — cancel KPIs, trend, reasons  

Issues relative to e-commerce merchandising needs:

- KPI cards can mislabel or duplicate totals; weak period-over-period UX  
- Single-layer revenue story (no clear Gross vs Net)  
- Cancel analytics siloed from sales performance  
- Ranking and share visuals underpowered for merchandising decisions  
- UI feels like chart demos, not a coherent sales analytics hub  

## 2. Goals

**Primary persona:** Marketing / merchandising (category and product performance).

**v1 must answer:**

1. Which products/categories drive Net revenue, volume, and share?  
2. How are Gross/Net and orders trending vs the previous equal-length period?  
3. Secondary: where cancellations and refunds hurt which products/categories?

**Success criteria (Definition of Done):**

1. Single hub page, three tabs, global filter, default last 30 days vs previous 30 days.  
2. Gross and Net metrics defined and shown side by side.  
3. Merch ranking for products and categories with share and period delta.  
4. Cancellation tab with rate, value, reasons, and top cancelled products/categories.  
5. Charts inventory below implemented (Chart.js).  
6. Sidebar: one menu entry; legacy Revenue routes redirect.  
7. Excel export for active tab + filter.  
8. Metric engine unit tests; no duplicated/wrong KPI labels from the old UI.  

## 3. Non-goals (v1)

- Revenue recognition by delivery/payment timestamp (requires reliable status history timestamps)  
- COGS / contribution margin  
- Pre-aggregated warehouse / nightly fact tables  
- Marketing attribution (UTM, campaigns)  
- Full “Giá & KM” analytics tab (deferred to v1.1)  
- Customer lifetime / cohort analytics  

## 4. Decisions log

| Topic | Choice |
|--------|--------|
| Persona | Merchandising |
| Scope | Core: product/category + trend/compare; Secondary: cancel/refund; Price/promo = v1.1 |
| IA | One hub “Thống kê bán hàng”, three tabs |
| Metrics | Gross (Paid) + Net (Delivered − Refund) |
| Default range | Last 30 days vs previous contiguous 30 days |
| Approach | Rebuild analytics domain + hub (not UI-only polish; not pre-aggregate jobs) |
| Visual | Variant A Classic, chart-dense |
| Time basis v1 | `Order.CreatedAt` (store Vietnam convention, as today) |

## 5. Metric dictionary

### 5.1 Order sets (within period)

| Set | Rule |
|-----|------|
| Paid | `PaymentStatus == Paid` |
| Delivered | `PaymentStatus == Paid` AND `OrderStatus == Delivered` |
| Refund | `PaymentStatus == Refunded` (typically with `Returned`) |
| Cancelled | `OrderStatus == Cancelled` |
| All orders | All orders with `CreatedAt` in period (denominator for cancel rate) |

### 5.2 Money and volume

| Metric | Formula (v1) |
|--------|----------------|
| Gross revenue | `Sum(Total)` of Paid set |
| Net revenue | `Sum(Total)` of Delivered set − `Sum(Total)` of Refund set |
| Discount amount | `Sum(Discount)` of Paid set (for export / future tab) |
| Shipping collected | `Sum(ShippingFee)` of Paid set |
| Merchandise subtotal | `Sum(Subtotal)` of Paid set |
| AOV gross | Gross / count(Paid), 0 if empty |
| AOV net | Net / count(Delivered), 0 if empty |
| Orders paid | count(Paid) |
| Orders delivered | count(Delivered) |
| Units sold (net) | Sum of line quantities on Delivered set orders |
| Cancel rate | count(Cancelled) / count(All orders) × 100 |
| Cancelled value | `Sum(Total)` of Cancelled set |
| Refund rate | count(Refund) / count(Paid) × 100 if Paid &gt; 0 else 0 |

### 5.3 Product / category ranking

- Aggregate from `OrderItem` lines belonging to orders in the relevant set.  
- **Primary rank metric:** Net line revenue = `Price × Quantity` on lines of Delivered orders (refund set excluded from net lines; do not reverse-allocate ship/discount to lines in v1).  
- **Share %** = line Net / total line Net in period.  
- **Secondary:** units, order count (distinct orders containing the product).  
- **Bottom performers:** sort Net ascending with `minOrders ≥ 1`.  
- Category uses product’s category at line time via current product→category join (document limitation if category changes).

### 5.4 Period-over-period

- `AnalyticsDateRange`: `[StartInclusive, EndExclusive)` in Vietnam store time.  
- Previous period: same length, immediately before current, no overlap.  
- Each core KPI: `Value`, `Previous`, `Delta`, `DeltaPercent`.  
- If previous = 0 and current &gt; 0: `DeltaPercent = null` (UI shows “mới” / “—”), never divide by zero.

### 5.5 Explicit limitation

v1 bucketing uses **order created date**, not paid/delivered event date. Documented in UI tooltip/help near Gross/Net labels.

## 6. Information architecture

### 6.1 Navigation

- Replace sidebar items “Doanh thu” and “Đơn hủy” with **Thống kê bán hàng**.  
- Routes:
  - `GET /Admin/Analytics` — hub (`tab`, `preset`, `from`, `to`)  
  - `GET /Admin/Analytics/Export` — Excel  
  - Optional JSON: trend grain refresh only  
  - `GET /Admin/Revenue` → redirect hub `tab=overview`  
  - `GET /Admin/Revenue/CancelledOrders` → redirect hub `tab=cancellations`  

### 6.2 Page chrome (Variant A Classic)

```
Header: title + export
Sticky global filter: presets + custom dates + read-only “so với {previous label}”
Tabs: Tổng quan | Sản phẩm & danh mục | Hủy & hoàn
Tab body
```

Default: `preset=Last30Days`, `tab=overview`.

### 6.3 Tab: Tổng quan

1. **KPI strip (6):** Gross, Net, Δ Net (amount or %), Orders paid, AOV net, Cancel rate — each with previous-period delta.  
2. **Charts (required v1):**
   - Gross / Net trend (line)  
   - Paid vs Cancelled by day (bar)  
   - Category mix doughnut (Net share)  
   - AOV Gross vs Net (line)  
   - Units net (bar)  
   - Pipeline by status (bar)  
   - Period compare grouped bar (Gross / Net / Orders)  
   - Top products horizontal bar  
3. **Tables:** Top 5 products + Top 5 categories with “Xem tất cả” → merch tab.

### 6.4 Tab: Sản phẩm & danh mục

1. Dimension toggle: Product | Category.  
2. **Charts:** ranking h-bar, category doughnut, units vs Net dual-axis, growth % bars.  
3. **Full ranking table:** rank, name, units, Net, share %, orders, Δ %. Sortable. Default top 50, max 200.  

### 6.5 Tab: Hủy & hoàn

1. KPIs: cancelled count, cancel rate, cancelled value, refund rate.  
2. **Charts:** cancel count + rate combo, reason doughnut, value lost by product (h-bar), cancel value by category.  
3. Supporting small tables optional if chart labels insufficient.

### 6.6 Giá & KM

Hidden or “Sắp có” in v1. Not blocking.

## 7. Architecture

### 7.1 Components

```
AnalyticsController (Area Admin)
    → ISalesAnalyticsService
         → SalesMetricEngine (pure set + formula rules)
         → overview / merch / cancellation query methods
    → IUnitOfWork (Orders, OrderItems, Categories, Products)
```

v1 may implement a single `SalesAnalyticsService` with private helpers; split files if a file exceeds ~400 lines.

### 7.2 Key types

- `AnalyticsDateRange`  
- `AnalyticsPeriodPair`  
- `MetricValue` (Value, Previous, Delta, DeltaPercent)  
- `SalesOverviewVm`, `SalesMerchVm`, `SalesCancellationsVm`, `SalesHubVm`  
- Reuse / extend `DateRangePreset` and `ToDateRange()`; add `ToPeriodPair()`.

### 7.3 Data loading

- Prefer **SSR** full tab on filter/tab change (simple admin UX).  
- AJAX only for trend **grain** (Day/Week/Month) on overview, matching existing pattern.  
- Always `AsNoTracking()`. Project to DTOs before grouping.  
- SQLite: materialize decimals then `Sum` in memory (existing constraint).  

### 7.4 Validation

- Custom range: start ≤ end; max span 366 days.  
- Invalid → 400 or hub re-render with inline error + toast; do not crash charts.  

### 7.5 Export

ClosedXML workbook:

1. Sheet “Định nghĩa metric” (Gross/Net rules, period labels)  
2. Sheet data for active tab  
Filename: `ThongKeBanHang_{tab}_{yyyyMMdd}_{yyyyMMdd}.xlsx`

### 7.6 Deprecation

- New DI registrations for `ISalesAnalyticsService`.  
- Migrate any Dashboard widgets that call old revenue overview to the new facade subset.  
- Remove or obsolete `IRevenueStatisticsService` / `ICancelledOrdersStatisticsService` implementations once callers are migrated (same feature PR series).  

## 8. UI / UX

- Align with Admin Fruitables: Be Vietnam Pro, accent `#81c408`, cards ~12px radius, Font Awesome icons (no emoji).  
- Numbers: `N0` + `đ`, tabular nums.  
- Gross = slate tone; Net = green; cancel = danger.  
- Delta: up green / down red / flat muted; never color-only (include ↑↓ text).  
- **Loading:** skeleton for KPI + chart cards.  
- **Empty period:** zeros + empty chart/table copy.  
- **Partial pipeline:** Gross &gt; 0 and Net = 0 is valid; tooltip explains Net.  
- **Errors:** toast (no `alert`).  
- Tabs: keyboard accessible tablist pattern.  
- Filter sticky under page header; mobile: collapsible filter.  
- Chart.js; honor `prefers-reduced-motion` (disable animations).  
- Visual reference: prototype Variant A (charts-heavy).  

## 9. Testing

### 9.1 Unit — SalesMetricEngine / date pair

- Fixture orders covering Paid-only, Delivered, Refund, Cancelled.  
- Gross/Net/cancel rate/refund rate expectations.  
- Previous period length and non-overlap.  
- DeltaPercent null when previous is zero.  
- Invalid date range rejected.  

### 9.2 Service integration

- Seed SQLite/in-memory; overview totals match hand-calculated fixtures.  
- Merch ranking totals reconcile to sum of line Net.  
- Redirects from legacy Revenue URLs.  

### 9.3 UI smoke

- Default 30d overview loads with charts.  
- Tab switch keeps filter query.  
- Empty state for period with no orders.  
- Export returns 200 and xlsx content type.  

## 10. Implementation outline (for later planning)

1. Metric engine + period pair helpers + tests.  
2. `ISalesAnalyticsService` overview/merch/cancellations.  
3. `AnalyticsController` + hub view + filter partial.  
4. Chart.js modules per tab (shared colors/helpers).  
5. Sidebar + redirects + export.  
6. Migrate/remove old revenue/cancel services and Dashboard callers.  
7. Playwright or manual QA checklist.  

## 11. Risks

| Risk | Mitigation |
|------|------------|
| SQLite decimal Sum | In-memory sum after materialize |
| CreatedAt ≠ economic event date | Document; phase 2 recognition dates |
| Chart overload | Inventory fixed to merch-useful set; no extra vanity charts without product ask |
| Large custom ranges | 366-day cap; row caps on merch table |
| Old bookmarks | Redirects preserve cancel intent via `tab=` |

## 12. Prototype

Throwaway UI explorations live at:

`docs/superpowers/prototypes/sales-analytics-hub/index.html`

Open in browser; switch `?variant=a|b|c`. **Production follows Variant A** structure and the chart list in §6.

## 13. Open items (resolved for v1)

| Item | Resolution |
|------|------------|
| Route name | `/Admin/Analytics` |
| Default visual | Classic A |
| Chart density | Full inventory in §6 |
| Price/promo tab | v1.1 |
| Recognition date | Out of v1 |

---

**Next step after approval of this file:** create implementation plan via writing-plans skill (do not implement until plan exists and is agreed).
