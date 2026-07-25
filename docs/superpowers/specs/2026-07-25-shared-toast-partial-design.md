# Shared Toast Partial Design

## Goal

Render each server-side TempData notification exactly once per page while retaining existing dynamic JavaScript toasts.

## Scope

This change covers the storefront layout, the Admin dashboard layout, and pages that duplicate their layouts' TempData toast markup. It does not change notification copy, controller behavior, SignalR notifications, or the data stored in TempData.

## Architecture

Create `Views/Shared/_ToastNotifications.cshtml` as the sole renderer for server-side toast notifications. The partial owns one Bootstrap `.toast-container` and renders, at most, one toast for each of the four semantic levels: success, error, warning, and info.

Both `Views/Shared/_Layout.cshtml` and `Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml` render the shared partial once. Page-level TempData toast markup is removed, so a page cannot render the same `TempData` value again after the layout has consumed it.

The partial accepts the current key families without controller changes:

| Semantic level | Preferred key | Backward-compatible key |
| --- | --- | --- |
| Success | `Success` | `SuccessMessage` |
| Error | `Error` | `ErrorMessage` |
| Warning | `Warning` | — |
| Info | `Info` | — |

When both keys exist for the same semantic level, the preferred key wins and only one toast is rendered. The partial uses Bootstrap-compatible classes and `data-bs-autohide="true"`; the existing layout-level Bootstrap initializer owns showing those elements.

## JavaScript Rules

Dynamic AJAX toasts keep appending to the container rendered by the partial. The container has a stable id, `toastContainer`, in addition to the existing `.toast-container` class so older scripts continue to work.

Only layouts initialize existing server-rendered `.toast` elements. Page-level scripts that scan and initialize every `.toast` are removed where the page uses `_AdminDashboardLayout` or `_Layout`; dynamic toast helper functions remain unchanged.

## Affected Pages

Remove local TempData toast containers from the Admin pages that explicitly use `_AdminDashboardLayout`: Category Index/Trash, ChatLog Index, Coupon Index, Faq Index, Order Index, Permission Index, Product Trash, RbacAudit Index, Role Detail/Index, Settings Index, User Detail/Index.

Remove local TempData toast containers and their duplicate initializers from `Views/OrderHistory/Index.cshtml` and `Views/OrderHistory/Details.cshtml`.

Views using `_AdminLayout`, `_AuthLayout`, or `Layout = null` are out of scope because those layouts do not render the shared partial.

## Failure and Compatibility Behavior

Missing TempData values result in an empty container and no visible toast. The partial HTML-encodes all messages through Razor output. Existing controllers can continue setting either supported key family during this refactor.

## Verification

Add Razor view tests or equivalent rendering tests that verify key precedence and a single container. Add focused page/source tests to ensure migrated pages no longer include a second TempData toast container. Manually verify: a successful Admin operation, a failed Admin operation, cancelling an order from both Order History pages, and an AJAX toast on each layout.
