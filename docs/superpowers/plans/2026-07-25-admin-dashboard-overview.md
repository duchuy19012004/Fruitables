# Admin Dashboard Overview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Promote prototype A into the production Admin dashboard: concise visibility of monthly revenue, today’s orders, low-stock risk, recent orders, and essential actions.

**Architecture:** Keep `DashboardController` and `IDashboardService` unchanged as the source of `DashboardViewModel`. Replace the `?variant=` prototype switch with a single `Index.cshtml` composition of three focused Razor partials; retain `_AdminDashboardLayout.cshtml` for the shell and global Be Vietnam Pro font.

**Tech Stack:** ASP.NET Core 8 MVC, Razor partials, Bootstrap 5, Font Awesome 5, Be Vietnam Pro, xUnit, Moq.

## Global Constraints

- No new dependencies or JavaScript framework.
- Consume the existing, real `DashboardViewModel`; the dashboard stays read-only.
- Use `Be Vietnam Pro` everywhere in the dashboard; do not add another font, emoji, or serif UI typeface.
- Preserve Admin/SuperAdmin authorization and all existing routes.
- Remove every variant, `?variant=` branch, and the bottom switcher before release.
- Empty orders and zero-value metrics must render safely.

---

## File Structure

- Modify: `Areas/Admin/Views/Dashboard/Index.cshtml` — production composition, styles, SignalR refresh.
- Create: `Areas/Admin/Views/Dashboard/_OverviewKpis.cshtml` — revenue, today’s orders, low-stock KPIs.
- Create: `Areas/Admin/Views/Dashboard/_RecentOrdersPanel.cshtml` — the latest five orders and empty state.
- Create: `Areas/Admin/Views/Dashboard/_OverviewActions.cshtml` — processing pulse and navigation links.
- Delete: `Areas/Admin/Views/Dashboard/_PrototypeOverview.cshtml`, `_PrototypeOperations.cshtml`, `_PrototypeFocus.cshtml`, and `Areas/Admin/Views/Shared/_PrototypeSwitcher.cshtml`.
- Create: `Tests/DashboardControllerTests.cs` — controller contract test.

### Task 1: Protect the current controller contract

**Files:**
- Create: `Tests/DashboardControllerTests.cs`
- Test: `Tests/DashboardControllerTests.cs`

**Interfaces:**
- Consumes: `IDashboardService.GetDashboardDataAsync(ChartPeriod.Last7Days, 10)`.
- Produces: a regression test proving `DashboardController.Index()` forwards the service model into a view.

- [ ] **Step 1: Write the test**

```csharp
using Fruitables.Areas.Admin.Controllers;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class DashboardControllerTests
{
    [Fact]
    public async Task Index_ReturnsDashboardViewWithServiceModel()
    {
        var expected = new DashboardViewModel
        {
            Orders = new OrderStatistics { TodayOrders = 3 },
            RecentOrders = new List<RecentOrderItem>()
        };
        var service = new Mock<IDashboardService>();
        service.Setup(x => x.GetDashboardDataAsync(ChartPeriod.Last7Days, 10)).ReturnsAsync(expected);
        var controller = new DashboardController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(expected, view.Model);
        service.Verify(x => x.GetDashboardDataAsync(ChartPeriod.Last7Days, 10), Times.Once);
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test Tests/Fruitables.Tests.csproj --no-restore --filter FullyQualifiedName~DashboardControllerTests`

Expected: PASS.

- [ ] **Step 3: Commit the test**

Run: `git add Tests/DashboardControllerTests.cs; git commit -m "test: cover dashboard controller view model"`

### Task 2: Promote A’s header and KPI hierarchy

**Files:**
- Create: `Areas/Admin/Views/Dashboard/_OverviewKpis.cshtml`
- Modify: `Areas/Admin/Views/Dashboard/Index.cshtml`

**Interfaces:**
- Consumes: `Revenue.MonthlyRevenue`, `MonthlyGrowthPercent`, `Orders.TodayOrders`, `Orders.PendingOrders`, `Inventory.LowStockProducts`, `Inventory.OutOfStockProducts`.
- Produces: one `overview-kpis` section; no query-string-driven rendering.

- [ ] **Step 1: Remove the prototype switch and compose the production header**

```cshtml
<div class="dashboard-overview">
    <header class="dashboard-overview__header">
        <div>
            <span class="dashboard-overview__eyebrow">Fruitables · Command center</span>
            <h1 class="dashboard-overview__title">Một ngày bán hàng, nhìn trong một nhịp.</h1>
            <p class="dashboard-overview__subtitle">Ưu tiên bức tranh doanh thu và đơn hàng mới nhất.</p>
        </div>
        <a asp-area="Admin" asp-controller="Product" asp-action="Create" class="dashboard-overview__primary-action"><i class="fas fa-plus me-2" aria-hidden="true"></i>Thêm sản phẩm</a>
    </header>
    <partial name="_OverviewKpis" model="Model" />
</div>
```

- [ ] **Step 2: Create `_OverviewKpis.cshtml`**

```cshtml
@model Fruitables.ViewModels.DashboardViewModel
@{ var sign = Model.Revenue.MonthlyGrowthPercent >= 0 ? "+" : string.Empty; }
<section class="overview-kpis" aria-label="Chỉ số vận hành">
    <article class="overview-kpi overview-kpi--revenue"><span>Doanh thu tháng</span><strong>@Model.Revenue.MonthlyRevenue.ToString("N0")đ</strong><small>@sign@Model.Revenue.MonthlyGrowthPercent.ToString("N1")% so với tháng trước</small></article>
    <article class="overview-kpi"><span>Đơn hôm nay</span><strong>@Model.Orders.TodayOrders</strong><small>@Model.Orders.PendingOrders đơn đang chờ xử lý</small></article>
    <article class="overview-kpi"><span>Kho cần chú ý</span><strong>@Model.Inventory.LowStockProducts</strong><small>@Model.Inventory.OutOfStockProducts sản phẩm đã hết hàng</small></article>
</section>
```

- [ ] **Step 3: Add the scoped CSS contract**

```css
.dashboard-overview, .dashboard-overview h1, .dashboard-overview p, .dashboard-overview strong, .dashboard-overview span, .dashboard-overview small, .dashboard-overview a { font-family:'Be Vietnam Pro', sans-serif; }
.overview-kpis { display:grid; grid-template-columns:1.35fr .9fr .9fr; gap:1rem; margin-top:1.75rem; }
.overview-kpi { background:#fff; border:1px solid #e4ebe4; min-height:148px; padding:1.25rem; }
.overview-kpi--revenue { background:#edf5e9; border-color:#d9e9d2; }
@media (max-width:991px) { .overview-kpis { grid-template-columns:1fr; } }
```

- [ ] **Step 4: Run test and build**

Run: `dotnet test Tests/Fruitables.Tests.csproj --no-restore --filter FullyQualifiedName~DashboardControllerTests`

Run: `dotnet build --no-restore --output C:\tmp\Fruitables-dashboard-build`

Expected: all tests pass; build has 0 errors.

- [ ] **Step 5: Commit**

Run: `git add Areas/Admin/Views/Dashboard/Index.cshtml Areas/Admin/Views/Dashboard/_OverviewKpis.cshtml; git commit -m "feat: promote dashboard overview kpis"`

### Task 3: Add recent orders and operating actions

**Files:**
- Create: `Areas/Admin/Views/Dashboard/_RecentOrdersPanel.cshtml`
- Create: `Areas/Admin/Views/Dashboard/_OverviewActions.cshtml`
- Modify: `Areas/Admin/Views/Dashboard/Index.cshtml`

**Interfaces:**
- Consumes: `RecentOrders`, `Orders.ProcessingOrders`, and existing Admin routes.
- Produces: a responsive `.overview-grid` with order detail links and an empty state.

- [ ] **Step 1: Create `_RecentOrdersPanel.cshtml`**

```cshtml
@model Fruitables.ViewModels.DashboardViewModel
<section class="overview-panel" aria-labelledby="recent-orders-title">
    <div class="overview-panel__head"><h2 id="recent-orders-title">Đơn hàng vừa cập nhật</h2><a asp-area="Admin" asp-controller="Order" asp-action="Index">Mở danh sách</a></div>
    @if (Model.RecentOrders.Any())
    {
        foreach (var order in Model.RecentOrders.Take(5))
        {
            <div class="recent-order"><a asp-area="Admin" asp-controller="Order" asp-action="Detail" asp-route-id="@order.Id">#@order.OrderNumber</a><span>@order.CustomerName</span><strong>@order.Total.ToString("N0")đ</strong><span class="badge @order.StatusBadgeClass">@order.Status</span></div>
        }
    }
    else { <p class="overview-panel__empty">Chưa có đơn hàng nào để hiển thị.</p> }
</section>
```

- [ ] **Step 2: Create `_OverviewActions.cshtml`**

```cshtml
@model Fruitables.ViewModels.DashboardViewModel
<aside class="overview-actions" aria-label="Tác vụ vận hành">
    <section class="overview-pulse"><span>Nhịp vận hành</span><strong>@Model.Orders.ProcessingOrders đơn đang xử lý</strong></section>
    <nav class="overview-action-links" aria-label="Đi tới tác vụ">
        <a asp-area="Admin" asp-controller="Product" asp-action="Index">Danh mục sản phẩm <i class="fas fa-arrow-right" aria-hidden="true"></i></a>
        <a asp-area="Admin" asp-controller="Settings" asp-action="Index">Cài đặt cửa hàng <i class="fas fa-arrow-right" aria-hidden="true"></i></a>
        <a href="/" target="_blank" rel="noopener">Xem cửa hàng <i class="fas fa-external-link-alt" aria-hidden="true"></i></a>
    </nav>
</aside>
```

- [ ] **Step 3: Add partials to the production page and its grid**

```cshtml
<section class="overview-grid" aria-label="Hoạt động gần đây">
    <partial name="_RecentOrdersPanel" model="Model" />
    <partial name="_OverviewActions" model="Model" />
</section>
```

Add `.overview-grid { display:grid; grid-template-columns:1.15fr .85fr; gap:1rem; margin-top:1rem; }` and a `max-width:991px` single-column override to the scoped CSS.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test Tests/Fruitables.Tests.csproj --no-restore --filter FullyQualifiedName~DashboardControllerTests; dotnet build --no-restore --output C:\tmp\Fruitables-dashboard-build`

Expected: PASS and 0 build errors.

Run: `git add Areas/Admin/Views/Dashboard/Index.cshtml Areas/Admin/Views/Dashboard/_RecentOrdersPanel.cshtml Areas/Admin/Views/Dashboard/_OverviewActions.cshtml; git commit -m "feat: add dashboard activity panels"`

### Task 4: Retire the prototype and accept the released page

**Files:**
- Delete: `Areas/Admin/Views/Dashboard/_PrototypeOverview.cshtml`
- Delete: `Areas/Admin/Views/Dashboard/_PrototypeOperations.cshtml`
- Delete: `Areas/Admin/Views/Dashboard/_PrototypeFocus.cshtml`
- Delete: `Areas/Admin/Views/Shared/_PrototypeSwitcher.cshtml`
- Modify: `Areas/Admin/Views/Dashboard/Index.cshtml`

**Interfaces:**
- Consumes: production partials from Tasks 2–3.
- Produces: a single clean `/Admin` dashboard with no prototype state.

- [ ] **Step 1: Delete the prototype files and their selectors**

Delete the four files. Remove all `PROTOTYPE` comments, `.dp-*` CSS selectors, the `variant` request read, and the fixed switcher markup/script from `Index.cshtml`. Keep the existing SignalR `OrderCreated` and `OrderStatusChanged` refresh handlers.

- [ ] **Step 2: Verify removal**

Run: `rg -n "Prototype|prototype|variant=|dp-" Areas/Admin/Views/Dashboard Areas/Admin/Views/Shared`

Expected: no matches.

- [ ] **Step 3: Run automated and visual acceptance**

Run: `dotnet test Tests/Fruitables.Tests.csproj --no-restore --filter FullyQualifiedName~DashboardControllerTests; dotnet build --no-restore --output C:\tmp\Fruitables-dashboard-build`

Expected: PASS and 0 build errors.

Open `http://localhost:5270/Admin` as Admin or SuperAdmin. Confirm the exact title uses Be Vietnam Pro, the switcher is absent, KPI values match live data, order links open the correct details, the empty state renders, and the layout stacks below 992px without horizontal scrolling.

- [ ] **Step 4: Commit the production release**

Run: `git add Areas/Admin/Views/Dashboard/Index.cshtml Areas/Admin/Views/Dashboard/_OverviewKpis.cshtml Areas/Admin/Views/Dashboard/_RecentOrdersPanel.cshtml Areas/Admin/Views/Dashboard/_OverviewActions.cshtml Tests/DashboardControllerTests.cs; git rm Areas/Admin/Views/Dashboard/_PrototypeOverview.cshtml Areas/Admin/Views/Dashboard/_PrototypeOperations.cshtml Areas/Admin/Views/Dashboard/_PrototypeFocus.cshtml Areas/Admin/Views/Shared/_PrototypeSwitcher.cshtml; git commit -m "feat: ship admin dashboard overview"`

## Plan Self-Review

- Prototype A’s hierarchy, real data, actions, mobile behavior, typography, and empty state are all covered.
- Tasks remove the temporary query switcher instead of shipping prototype code.
- The only new behavior test covers the controller-to-view model boundary; no service or database change is needed.
