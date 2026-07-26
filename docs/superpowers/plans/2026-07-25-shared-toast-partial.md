# Shared Toast Partial Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render every TempData notification once per page through a shared Razor partial, while keeping dynamic AJAX toasts functional.

**Architecture:** A pure factory maps existing TempData key families into semantic notification records. A shared partial renders that collection into one stable Bootstrap container. Both active layouts render the partial; pages delete only duplicate server-rendered markup and duplicate Bootstrap initializers.

**Tech Stack:** ASP.NET Core 8 MVC/Razor, Bootstrap 5, xUnit 2.5, Moq 4.20.

## Global Constraints

- Use a Razor partial, not a ViewComponent or new DI registration.
- Preserve the controller-facing keys `Success`, `Error`, `Warning`, `Info`, `SuccessMessage`, and `ErrorMessage`.
- For one semantic type, the preferred key (`Success`/ `Error`) wins over its compatibility key.
- Render one `#toastContainer.toast-container` per page; dynamic JavaScript continues using `.toast-container`.
- Do not alter notification copy, controller actions, SignalR behavior, or unrelated chat code.
- Preserve Bootstrap autohide: Admin 2000 ms, storefront 3000 ms.
- The current test baseline has an unrelated compile blocker: `Tests/Chat/Fakes/FakeLlmClient.cs` does not implement `ILlmClient.GenerateAsync`. Resolve or isolate it before accepting a green full-suite result; do not fold it into this refactor.

---

## File Structure

- Create: `ViewModels/ToastNotificationViewModel.cs` — immutable toast type and pure TempData-to-toast factory.
- Create: `Views/Shared/_ToastNotifications.cshtml` — the one Bootstrap container and server-side toast renderer.
- Create: `Tests/Views/ToastNotificationFactoryTests.cs` — factory precedence and empty-input coverage.
- Create: `Tests/Views/ToastPartialSourceTests.cs` — layout and page-migration regression checks.
- Modify: `Views/Shared/_Layout.cshtml` and `Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml`.
- Modify: fourteen Admin pages and two OrderHistory pages that currently duplicate their layouts' TempData markup.

## Task 1: Define and prove TempData-to-toast mapping

**Files:**
- Create: `ViewModels/ToastNotificationViewModel.cs`
- Create: `Tests/Views/ToastNotificationFactoryTests.cs`

**Interfaces:**
- Consumes: `Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary`.
- Produces: `ToastNotificationViewModel(string Level, string Message)` and `ToastNotificationFactory.Create(ITempDataDictionary)`, returning `IReadOnlyList<ToastNotificationViewModel>`.

- [ ] **Step 1: Write failing factory tests**

```csharp
[Fact]
public void Create_prefers_Success_over_SuccessMessage()
{
    var tempData = CreateTempData();
    tempData["Success"] = "New message";
    tempData["SuccessMessage"] = "Old message";

    var toast = Assert.Single(ToastNotificationFactory.Create(tempData));

    Assert.Equal("success", toast.Level);
    Assert.Equal("New message", toast.Message);
}

[Fact]
public void Create_maps_all_supported_levels_once()
{
    var tempData = CreateTempData();
    tempData["SuccessMessage"] = "Saved";
    tempData["Error"] = "Failed";
    tempData["Warning"] = "Check stock";
    tempData["Info"] = "Updated";

    var result = ToastNotificationFactory.Create(tempData);

    Assert.Collection(result,
        toast => Assert.Equal(("success", "Saved"), (toast.Level, toast.Message)),
        toast => Assert.Equal(("error", "Failed"), (toast.Level, toast.Message)),
        toast => Assert.Equal(("warning", "Check stock"), (toast.Level, toast.Message)),
        toast => Assert.Equal(("info", "Updated"), (toast.Level, toast.Message)));
}
```

Add empty and whitespace-only values and assert they produce no toast. Build `CreateTempData()` with `TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/Fruitables.Tests.csproj --filter FullyQualifiedName~ToastNotificationFactoryTests`

Expected: compile failure because the factory and model do not exist. If the known `FakeLlmClient` blocker occurs first, record it as baseline and run again after that unrelated change is available.

- [ ] **Step 3: Implement the pure factory**

```csharp
public sealed record ToastNotificationViewModel(string Level, string Message);

public static class ToastNotificationFactory
{
    public static IReadOnlyList<ToastNotificationViewModel> Create(ITempDataDictionary tempData)
    {
        var toasts = new List<ToastNotificationViewModel>();
        Add(toasts, "success", tempData["Success"] ?? tempData["SuccessMessage"]);
        Add(toasts, "error", tempData["Error"] ?? tempData["ErrorMessage"]);
        Add(toasts, "warning", tempData["Warning"]);
        Add(toasts, "info", tempData["Info"]);
        return toasts;
    }
}
```

`Add` converts only non-empty values to strings and trims before storing the message.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests/Fruitables.Tests.csproj --filter FullyQualifiedName~ToastNotificationFactoryTests`

Expected: all factory cases pass once the pre-existing Chat fake compile blocker is resolved.

- [ ] **Step 5: Commit**

```bash
git add ViewModels/ToastNotificationViewModel.cs Tests/Views/ToastNotificationFactoryTests.cs
git commit -m "feat: add shared toast notification mapping"
```

## Task 2: Render the shared partial from both layouts

**Files:**
- Create: `Views/Shared/_ToastNotifications.cshtml`
- Modify: `Views/Shared/_Layout.cshtml`
- Modify: `Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml`
- Create: `Tests/Views/ToastPartialSourceTests.cs`

**Interfaces:**
- Consumes: `ToastNotificationFactory.Create(TempData)`.
- Produces: exactly one `<div id="toastContainer" class="toast-container ...">` and zero to four Bootstrap `.toast` elements.

- [ ] **Step 1: Write a failing layout/partial regression test**

Read the three Razor files from the solution root derived from `AppContext.BaseDirectory`. Assert both layouts contain `PartialAsync("_ToastNotifications")`, neither contains inline `TempData["SuccessMessage"]` or `TempData["ErrorMessage"]`, and the partial contains exactly one `id="toastContainer"`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/Fruitables.Tests.csproj --filter FullyQualifiedName~ToastPartialSourceTests`

Expected: FAIL because the partial and layout references do not exist.

- [ ] **Step 3: Create the partial and replace layout markup**

The partial begins with:

```cshtml
@using Fruitables.ViewModels
@{
    var toasts = ToastNotificationFactory.Create(TempData);
}
<div id="toastContainer" class="toast-container position-fixed bottom-0 end-0 p-3" style="z-index: 1100;">
```

Render each record with a Bootstrap class selected by `Level`, Razor-encoded `@toast.Message`, a dismiss button, and the existing delay. Before calling the partial, set `ViewData["ToastDelay"]` to `3000` in storefront and `2000` in Admin. Replace inline TempData blocks with one partial call and retain the existing layout `DOMContentLoaded` initializer as the only initializer.

- [ ] **Step 4: Run focused tests and compile**

Run: `dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~ToastNotificationFactoryTests|FullyQualifiedName~ToastPartialSourceTests"`

Run: `dotnet build Fruitables.csproj --no-restore`

Expected: focused tests pass and the web project compiles. Do not claim green if the unrelated Chat fake still blocks the test project.

- [ ] **Step 5: Commit**

```bash
git add Views/Shared/_ToastNotifications.cshtml Views/Shared/_Layout.cshtml Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml Tests/Views/ToastPartialSourceTests.cs
git commit -m "refactor: render TempData toasts through shared partial"
```

## Task 3: Remove duplicate page-level renderers and initializers

**Files:**
- Modify: `Areas/Admin/Views/Category/Index.cshtml`, `Category/Trash.cshtml`, `ChatLog/Index.cshtml`, `Coupon/Index.cshtml`, `Faq/Index.cshtml`, `Order/Index.cshtml`, `Permission/Index.cshtml`, `Product/Trash.cshtml`, `RbacAudit/Index.cshtml`, `Role/Detail.cshtml`, `Role/Index.cshtml`, `Settings/Index.cshtml`, `User/Detail.cshtml`, and `User/Index.cshtml`.
- Modify: `Views/OrderHistory/Index.cshtml` and `Views/OrderHistory/Details.cshtml`.
- Modify: `Tests/Views/ToastPartialSourceTests.cs`.

**Interfaces:**
- Consumes: the active layout's container via `.toast-container` or `#toastContainer`.
- Produces: no page-level reads of `TempData["Success"]`, `["Error"]`, `["SuccessMessage"]`, or `["ErrorMessage"]` solely for toast markup.

- [ ] **Step 1: Extend the failing source regression test**

Create a list containing all sixteen page paths above. For each, assert the source no longer contains a server-rendered toast container or any of the four TempData keys. For pages with a bulk `document.querySelectorAll('.toast')` initializer, assert that initializer is absent.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/Fruitables.Tests.csproj --filter FullyQualifiedName~ToastPartialSourceTests`

Expected: FAIL and name every page that still owns server-side TempData toast markup.

- [ ] **Step 3: Remove only duplicate code**

Delete the local TempData toast containers. Delete only page-level bulk initializers shaped as `document.querySelectorAll('.toast')` followed by `new bootstrap.Toast(...)`. Retain dynamic AJAX helpers such as `showToast(...)`; they append new toast elements to the shared partial container.

- [ ] **Step 4: Run regression tests and smoke checks**

Run: `dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~ToastNotificationFactoryTests|FullyQualifiedName~ToastPartialSourceTests"`

Manual checks:

1. Perform a successful and failed Admin action; each redirect displays one toast.
2. Cancel an order from Order History Index and Details; each redirect displays one toast.
3. Trigger an existing AJAX toast in Admin User/Role/Permission and in Address or Cart; it appears in the shared container and autohides.
4. Inspect each checked page: exactly one `#toastContainer` exists.

- [ ] **Step 5: Run full tests and commit**

Run: `dotnet test Tests/Fruitables.Tests.csproj --no-restore`

Expected: pass after the pre-existing Chat fake mismatch is separately addressed; otherwise report that exact blocker without changing unrelated chat code.

```bash
git add Areas/Admin/Views Views/OrderHistory Tests/Views/ToastPartialSourceTests.cs
git commit -m "refactor: remove duplicate page toast renderers"
```

## Plan Self-Review

- Spec coverage: Tasks 1–2 implement the shared partial, compatible keys, key precedence, one container, and layout-only initialization. Task 3 removes all sixteen audited duplicate renderers and preserves AJAX behavior.
- Placeholder scan: no deferred implementation or unspecified page list remains.
- Type consistency: `ToastNotificationFactory.Create(ITempDataDictionary)` produces the records consumed only by the partial; no controller or JavaScript API changes are required.

