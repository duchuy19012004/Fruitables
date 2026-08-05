# Remove Return Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the current return/refund module and every application integration so the feature can be rebuilt from a clean baseline.

**Architecture:** Delete return-only layers first, then remove their shared integration points from persistence, dependency injection, RBAC, outbox, order state, UI, and analytics. Preserve unrelated application behavior and pending worktree changes. The database may be reset, so return migration history and return tables are intentionally removed from the source model.

**Tech Stack:** ASP.NET Core MVC, .NET 8, Entity Framework Core, SQL Server migrations, xUnit.

## Global Constraints

- Do not run `git reset`, `git checkout`, or broad cleanup commands.
- Do not modify unrelated pending work.
- Do not commit automatically.
- Delete the current return/refund migration history because the return database may be reset.
- Preserve shared code unless its only usage is return/refund functionality.
- Preserve generic cancellation/refund behavior that still uses `PaymentStatus.Refunded`; remove only return-only payment status members and branches.
- Keep `docs/superpowers/specs/2026-08-05-remove-return-module-design.md` as the removal record.

---

### Task 1: Delete Return-Only Layers

**Files:**
- Delete: `Models/Returns/` (`*.cs`)
- Delete: `Services/Returns/` (`**/*.cs`)
- Delete: `ViewModels/Returns/` (`*.cs`)
- Delete: `Controllers/ReturnController.cs`
- Delete: `Controllers/ReturnEvidenceController.cs`
- Delete: `Areas/Admin/Controllers/ReturnController.cs`
- Delete: `Areas/Admin/Controllers/RefundController.cs`
- Delete: `Areas/Admin/Controllers/ReturnPolicyController.cs`
- Delete: `Views/Return/` (`**/*.cshtml`)
- Delete: `Areas/Admin/Views/Return/` (`**/*`)
- Delete: `Areas/Admin/Views/Refund/` (`**/*`)
- Delete: `Areas/Admin/Views/ReturnPolicy/` (`**/*`)
- Delete: `Helpers/ReturnDisplay.cs`
- Delete: `wwwroot/css/return-admin.css`
- Delete: `Tests/ReturnDomainModelTests.cs`
- Delete: `Tests/ReturnDecisionWorkflowTests.cs`
- Delete: `Tests/ReturnControllerSecurityTests.cs`
- Delete: `Tests/ReturnSqlServerIntegrationTests.cs`
- Delete: `Tests/ReturnRiskAndAutoApprovalTests.cs`
- Delete: `Tests/ReturnPolicyConfigurationTests.cs`
- Delete: `Tests/ReturnModuleTests.cs`
- Delete: `Tests/ReturnEvidenceServiceTests.cs`
- Delete: `Tests/RefundAmountCalculatorFreshProduceTests.cs`
- Delete: `Migrations/20260727151845_AddReturnClaimsFoundation.cs`
- Delete: `Migrations/20260727151845_AddReturnClaimsFoundation.Designer.cs`
- Delete: `Migrations/20260727153529_ProtectInternalReturnEvidence.cs`
- Delete: `Migrations/20260727153529_ProtectInternalReturnEvidence.Designer.cs`
- Delete: `Migrations/20260727175418_AddRefundDestination.cs`
- Delete: `Migrations/20260727175418_AddRefundDestination.Designer.cs`
- Delete: `Migrations/20260801154819_AddFreshProduceReturnWorkflow.cs`
- Delete: `Migrations/20260801154819_AddFreshProduceReturnWorkflow.Designer.cs`
- Delete: `Migrations/20260801163641_AddReturnApprovalRuleSnapshots.cs`
- Delete: `Migrations/20260801163641_AddReturnApprovalRuleSnapshots.Designer.cs`
- Delete: `Migrations/20260802000218_AddReturnIntakeSnapshots.cs`
- Delete: `Migrations/20260802000218_AddReturnIntakeSnapshots.Designer.cs`
- Delete: `Migrations/20260802010002_AllowShippingOnlyRefund.cs`
- Delete: `Migrations/20260802010002_AllowShippingOnlyRefund.Designer.cs`
- Delete: `Migrations/20260802023000_AddReturnItemReopenCount.cs`
- Delete: `Migrations/20260802023000_AddReturnItemReopenCount.Designer.cs`
- Delete: `Migrations/ReturnDeliveredAtBackfillVerified.sql`
- Delete: `Migrations/ReturnDeliveredAtBackfillReport.sql`
- Delete: `docs/superpowers/specs/2026-08-01-fresh-produce-return-policy-design.md`
- Delete: `docs/superpowers/specs/2026-08-02-return-decision-workflow-design.md`
- Delete: `docs/superpowers/plans/2026-08-01-fresh-produce-return-policy-implementation.md`
- Delete: `docs/superpowers/plans/2026-08-02-return-decision-workflow.md`

**Interfaces:**
- Removes all return-specific namespaces and public types.
- Leaves the application intentionally uncompilable until Task 2 and Task 3 remove shared references.

- [ ] **Step 1: Delete only the listed return-only files and directories.** Use targeted file deletion; do not delete the entire `Migrations` directory or unrelated tests.
- [ ] **Step 2: Confirm the deletion set.** Run `git status --short` and verify every deleted path is in the approved scope; leave unrelated modified files untouched.

---

### Task 2: Remove Persistence, DI, RBAC, Outbox, and Email Wiring

**Files:**
- Modify: `Data/ApplicationDbContext.cs`
- Modify: `Migrations/ApplicationDbContextModelSnapshot.cs`
- Modify: `Program.cs`
- Modify: `Services/Infrastructure/MigrationService.cs`
- Modify: `Services/Outbox/IOutboxMessageHandler.cs`
- Modify: `Services/Outbox/OutboxMessageTypes.cs`
- Modify: `Services/Communications/IEmailService.cs`
- Modify: `Services/Communications/EmailService.cs`
- Modify: `Controllers/OrderHistoryController.cs`

**Interfaces:**
- `ApplicationDbContext` no longer exposes return `DbSet`s or configures return entities.
- DI no longer registers return services or the return outbox handler.
- The existing outbox interface and non-return handlers remain registered and functional.
- Email service retains unrelated email methods but removes `SendReturnNotificationEmailAsync` and its private body generator.
- `OrderHistoryController` keeps its existing constructor and behavior without `IReturnEligibilityService` or `ViewBag.ReturnEligibility`.

- [ ] **Step 1: Remove return `using` directives, DbSets, and `ConfigureReturns` invocation from `ApplicationDbContext.cs`.** Delete the `ConfigureReturns` method and only its return entity configuration; preserve all other model configuration.
- [ ] **Step 2: Remove return entity metadata from `Migrations/ApplicationDbContextModelSnapshot.cs`.** Delete return entity definitions, relationships, indexes, and return-owned properties; preserve all non-return entities and the snapshot namespace structure.
- [ ] **Step 3: Remove return service registrations from `Program.cs`.** Delete registrations for policies, eligibility, request/decision services, evidence, risk, refunds, disposition, and return approval support; preserve outbox, analytics, identity, catalog, and order registrations.
- [ ] **Step 4: Remove return seed code from `MigrationService.cs`.** Delete return role creation, return permission definitions/mappings, approval-rule seeding, calls to those methods, and the return model `using`; preserve generic roles, permissions, migrations, and address seeding.
- [ ] **Step 5: Remove only `ReturnDomainEventOutboxHandler` and return message constants.** Keep `IOutboxMessageHandler`, `SentimentAnalysisOutboxHandler`, and all non-return message types.
- [ ] **Step 6: Remove return notification email API and implementation.** Search for callers first; since the current repository has no callers outside `EmailService`, remove the interface method and matching implementation/helper without changing unrelated email templates.
- [ ] **Step 7: Remove return eligibility injection from `OrderHistoryController`.** Delete the optional field, constructor parameter, and details action assignment.

---

### Task 3: Remove Shared Order, Payment, Analytics, and UI References

**Files:**
- Modify: `Models/Order.cs`
- Modify: `ViewModels/OrderViewModels.cs`
- Modify: `Services/Orders/OrderManagement/OrderAdminService.cs`
- Modify: `Services/Reviews/ReviewService.cs`
- Modify: `Services/Sentiment/SentimentAnalysisService.cs`
- Modify: `Services/Analytics/Sales/SalesAnalyticsService.cs`
- Modify: `Areas/Admin/Controllers/OrderController.cs`
- Modify: `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`
- Modify: `Areas/Admin/Views/Order/Detail.cshtml`
- Modify: `Areas/Admin/Views/Order/Detail_Backup.cshtml`
- Modify: `Areas/Admin/Views/Order/_OrderList.cshtml`
- Modify: `Areas/Admin/Views/User/Detail.cshtml`
- Modify: `Views/OrderHistory/Index.cshtml`
- Modify: `Views/OrderHistory/Details.cshtml`
- Modify: `Views/OrderHistory/_OrderList.cshtml`
- Modify: `Views/Shop/Detail.cshtml`
- Modify: `Views/Home/Policies.cshtml`
- Modify: `Views/Shared/_Features.cshtml`
- Modify: `Views/Cart/Index.cshtml`
- Modify: `Tests/SalesMetricEngineTests.cs`
- Modify: `Tests/SalesAnalyticsServiceTests.cs`
- Modify: `Tests/OrderVariantStockTests.cs`

**Interfaces:**
- Order history no longer offers a return tab, return eligibility, return create links, or refund-only display branches.
- Admin navigation no longer exposes return, refund, or return-policy pages.
- Shared order state no longer depends on `OrderStatus.Returned` or `PaymentStatus.PartiallyRefunded`; generic cancellation/refund behavior using `PaymentStatus.Refunded` remains intact.
- Sales and combo analytics retain generic refund metrics; only `OrderStatus.Returned` fixtures/labels and return-specific branches are removed.
- Customer policy/feature copy no longer promises the removed return workflow.

- [ ] **Step 1: Remove return navigation and actions from order history views.** Preserve normal order status tabs, search, pagination, and AJAX behavior.
- [ ] **Step 2: Remove return/refund branches from admin order views and controller display helpers.** Delete the old return modal and links; preserve cancellation and generic `PaymentStatus.Refunded` behavior.
- [ ] **Step 3: Remove return-only order state and navigation properties.** Remove `Order.ReturnRequests`, `OrderStatus.Returned`, and `PaymentStatus.PartiallyRefunded`; preserve `PaymentStatus.Refunded` because the order cancellation workflow still uses it, then update remaining switch expressions and transition tables.
- [ ] **Step 4: Remove return-dependent filters from reviews and sentiment analytics.** Replace conditions that exclude `OrderStatus.Returned` with the remaining order lifecycle rules.
- [ ] **Step 5: Keep generic refund analytics intact while removing return-only status fixtures and labels.** Update only the directly affected sales tests and status formatter; do not delete generic refund-rate or combo revenue projections.
- [ ] **Step 6: Remove customer-facing return promises and static links.** Delete or rewrite only return-specific copy in shop detail, policies, feature cards, cart footer, and admin sidebar.
- [ ] **Step 7: Update directly affected tests.** Remove return-only cases and adjust shared order/analytics assertions to the remaining status set; do not delete unrelated test coverage.

---

### Task 4: Verify the Clean Baseline

**Files:**
- Modify only any shared file found to contain a missed return reference during verification.
- Preserve: `docs/superpowers/specs/2026-08-05-remove-return-module-design.md`

**Interfaces:**
- No return-specific type, namespace, route, permission, migration, view, or service remains in the build graph.

- [ ] **Step 1: Search for namespaces and entities.** Run:

```powershell
rg -n "Fruitables\.Models\.Returns|Fruitables\.Services\.Returns|ViewModels\.Returns|ReturnRequest|ReturnEvidence|ReturnPolicy|InventoryDisposition|RefundStatus|RefundMethod" --glob '!docs/superpowers/specs/2026-08-05-remove-return-module-design.md' .
```

Expected: no application/test/source matches; generic language `return` is allowed.

- [ ] **Step 2: Search for routes and permissions.** Run:

```powershell
rg -n "ReturnController|ReturnEvidenceController|RefundController|ReturnPolicyController|returns\.|asp-controller=\"Return|asp-controller=\"Refund|return-admin" --glob '!docs/superpowers/specs/2026-08-05-remove-return-module-design.md' .
```

Expected: no matches outside intentionally preserved historical git state.

- [ ] **Step 3: Build the application.** Run `dotnet build "Fruitables.csproj" --no-restore -v minimal` and require zero compile errors.
- [ ] **Step 4: Run tests.** Run `dotnet test "Tests\\Fruitables.Tests.csproj" --no-build -v minimal`; report failures caused by unrelated pre-existing changes separately.
- [ ] **Step 5: Inspect the final worktree.** Run `git status --short` and confirm only the approved deletion, shared cleanup, and the removal spec are present; do not stage or commit.
