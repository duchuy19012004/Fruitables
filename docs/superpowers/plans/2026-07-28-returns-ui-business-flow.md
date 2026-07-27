# Returns UI Business Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the return UI into one manual-refund journey for Customer, CSKH, and Finance, with one aggregate refund task and protected bank destination data.

**Architecture:** Keep the current ASP.NET Core MVC, EF Core, service, and outbox structure. `ReturnService` owns claim submission and the CSKH decision transaction; an approved decision creates one aggregate `Refund`. `RefundService` owns destination protection and the Finance lifecycle. Controllers expose role-specific UI and never accept an amount or return state from a form.

**Tech Stack:** .NET 8, ASP.NET Core MVC/Razor, EF Core 8, SQL Server, ASP.NET Core Data Protection, xUnit, Moq, Bootstrap, existing outbox infrastructure.

## Global Constraints

- Source of truth: `docs/superpowers/specs/2026-07-28-returns-ui-business-flow-design.md`.
- Run execution in an isolated worktree because the primary worktree contains unrelated changes.
- Preserve all existing numeric enum values; append new `ReturnEventType` values only at the end.
- Preserve ownership, eligibility, quantity, refund cap, shipping-fee, concurrency, antiforgery, audit, and UTC invariants.
- Store no full account number or account holder in plaintext, logs, TempData, outbox payloads, or email.
- Reuse the Data Protection key ring already configured in `Program.cs`; add no encryption or bank-list dependency.
- Keep replacement order, store credit, inventory disposition, payout API, notification, SLA, fraud, and analytics outside this plan.
- Keep existing outbox behavior; do not add a consumer.
- Use one aggregate refund per newly approved return request. Historical item-level refunds remain read-only.
- Do not modify old migrations. Add one forward migration and review its generated SQL.

---

## File map

### Domain and persistence

- `Models/Returns/Refund.cs`: protected destination fields for the aggregate refund.
- `Models/Returns/ReturnEnums.cs`: append destination and Finance audit event types.
- `Data/ApplicationDbContext.cs`: destination column lengths and existing refund indexes.
- `Migrations/*_AddRefundDestination.cs`: forward schema and RBAC data migration generated in Task 1.
- `Migrations/*_AddRefundDestination.Designer.cs`: generated migration metadata.
- `Migrations/ApplicationDbContextModelSnapshot.cs`: generated model snapshot.

### Business logic

- `Services/Returns/ReturnService.cs`: submit without requested resolution; decision creates the aggregate refund atomically.
- `Services/Returns/RefundService.cs`: protect destination data, claim Finance work, fail/retry, and confirm manual refunds.
- `Services/Interfaces/IRefundService.cs`: UI-facing refund lifecycle contract.
- `ViewModels/Returns/ReturnRequestViewModels.cs`: remove resolution fields and add CSKH queue buckets.
- `ViewModels/Returns/RefundViewModels.cs`: customer destination, Finance queue, detail, and failure input models.
- `Helpers/ReturnDisplay.cs`: customer progress labels and new audit labels.

### Customer UI

- `Controllers/ReturnController.cs`: destination POST and simplified claim flow.
- `Views/Return/Create.cshtml`: one-page item, reason, description, and evidence form.
- `Views/Return/Details.cshtml`: action-first status, evidence retry, and destination form.
- `Views/Return/Index.cshtml`: business progress labels.
- `Views/Return/_StatusTimeline.cshtml`: customer-safe audit copy.

### CSKH UI

- `Areas/Admin/Controllers/ReturnController.cs`: claim review actions only.
- `Areas/Admin/Views/Return/Index.cshtml`: business queue tabs.
- `Areas/Admin/Views/Return/_ReturnQueue.cshtml`: action-focused queue rows.
- `Areas/Admin/Views/Return/Detail.cshtml`: evidence and decision only.
- `Areas/Admin/Views/Return/_DecisionForm.cshtml`: approved quantity without resolution or amount inputs.

### Finance UI

- `Areas/Admin/Controllers/RefundController.cs`: Finance queue, claim, failure, and confirmation actions.
- `Areas/Admin/Views/Refund/Index.cshtml`: Finance queue tabs.
- `Areas/Admin/Views/Refund/Detail.cshtml`: fixed amount, protected destination, and proof workflow.
- `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`: permission-aware CSKH and Finance links.
- `wwwroot/css/return-admin.css`: shared queue and action-card styles.

### Tests and rollout

- `Tests/ReturnModuleTests.cs`: domain, aggregate refund, protection, and lifecycle tests.
- `Tests/ReturnControllerSecurityTests.cs`: antiforgery and permission tests for both admin controllers.
- `Tests/ReturnSqlServerIntegrationTests.cs`: concurrent decision and aggregate refund invariant.
- `docs/returns/refund-ui-rollout.md`: legacy-data report, migration rehearsal, role assignment, and rollback gate.

---

### Task 1: Add protected destination schema and role separation

**Files:**
- Modify: `Models/Returns/Refund.cs`
- Modify: `Models/Returns/ReturnEnums.cs`
- Modify: `Helpers/ReturnDisplay.cs`
- Modify: `Data/ApplicationDbContext.cs:717-728`
- Modify: `Tests/ReturnModuleTests.cs`
- Create via EF: `Migrations/*_AddRefundDestination.cs`
- Create via EF: `Migrations/*_AddRefundDestination.Designer.cs`
- Modify via EF: `Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: existing `Refund`, `ReturnEventType`, `Role`, `Permission`, and Data Protection registration.
- Produces: `Refund.DestinationBankCode`, `DestinationAccountNumberProtected`, `DestinationAccountLast4`, `DestinationAccountHolderProtected`, `DestinationSubmittedAtUtc`; appended audit event values.

- [ ] **Step 1: Write the failing model test**

Add to `Tests/ReturnModuleTests.cs`:

```csharp
[Fact]
public async Task RefundDestinationModel_DefinesProtectedColumnsWithoutRenumberingEvents()
{
    await using var db = CreateContext();
    var refund = db.Model.FindEntityType(typeof(Refund))!;

    Assert.Equal(50, refund.FindProperty(nameof(Refund.DestinationBankCode))!.GetMaxLength());
    Assert.Equal(1000, refund.FindProperty(nameof(Refund.DestinationAccountNumberProtected))!.GetMaxLength());
    Assert.Equal(4, refund.FindProperty(nameof(Refund.DestinationAccountLast4))!.GetMaxLength());
    Assert.Equal(1000, refund.FindProperty(nameof(Refund.DestinationAccountHolderProtected))!.GetMaxLength());
    Assert.Equal(15, (int)ReturnEventType.DispositionRecorded);
}
```

- [ ] **Step 2: Run the test and confirm it fails to compile**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests.RefundDestinationModel_DefinesProtectedColumnsWithoutRenumberingEvents"
```

Expected: FAIL because the destination properties do not exist.

- [ ] **Step 3: Add the destination fields and audit enum values**

Add to `Refund`:

```csharp
[MaxLength(50)] public string? DestinationBankCode { get; set; }
[MaxLength(1000)] public string? DestinationAccountNumberProtected { get; set; }
[MaxLength(4)] public string? DestinationAccountLast4 { get; set; }
[MaxLength(1000)] public string? DestinationAccountHolderProtected { get; set; }
public DateTime? DestinationSubmittedAtUtc { get; set; }
```

Append after `DispositionRecorded` in `ReturnEventType` without editing earlier members:

```csharp
RefundDestinationSubmitted,
RefundDestinationViewed,
RefundProcessingStarted,
RefundDestinationCorrectionRequested
```

Add these labels to `ReturnDisplay.Text(ReturnEventType)`:

```csharp
ReturnEventType.RefundDestinationSubmitted => "Khách hàng đã cung cấp thông tin nhận tiền",
ReturnEventType.RefundDestinationViewed => "Bộ phận tài chính đã xem thông tin nhận tiền",
ReturnEventType.RefundProcessingStarted => "Bộ phận tài chính bắt đầu xử lý",
ReturnEventType.RefundDestinationCorrectionRequested => "Yêu cầu cập nhật thông tin nhận tiền",
```

In `ConfigureReturns`, make all destination lengths explicit:

```csharp
entity.Property(x => x.DestinationBankCode).HasMaxLength(50);
entity.Property(x => x.DestinationAccountNumberProtected).HasMaxLength(1000);
entity.Property(x => x.DestinationAccountLast4).HasMaxLength(4);
entity.Property(x => x.DestinationAccountHolderProtected).HasMaxLength(1000);
```

- [ ] **Step 4: Generate the forward migration**

Run:

```bash
dotnet ef migrations add AddRefundDestination \
  --project Fruitables.csproj \
  --startup-project Fruitables.csproj
```

Expected: EF creates the two `AddRefundDestination` files and updates `ApplicationDbContextModelSnapshot.cs`. The migration adds five nullable columns and does not alter existing return columns or enum values.

- [ ] **Step 5: Add RBAC data changes to the generated migration**

At the end of `Up`, add SQL that ensures the CustomerSupport and Finance roles exist, grants their exact return permissions, and removes refund access from generic Admin:

```csharp
migrationBuilder.Sql("""
DECLARE @now datetime2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'CustomerSupport')
    INSERT INTO Roles(Name, Description, IsActive, CreatedAt, UpdatedAt)
    VALUES ('CustomerSupport', N'Nhân viên chăm sóc khách hàng', 1, @now, @now);

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Finance')
    INSERT INTO Roles(Name, Description, IsActive, CreatedAt, UpdatedAt)
    VALUES ('Finance', N'Nhân viên tài chính xử lý hoàn tiền', 1, @now, @now);

INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id, p.Id, @now, NULL
FROM Roles r CROSS JOIN Permissions p
WHERE r.Name = 'CustomerSupport'
  AND p.Name IN ('returns.view', 'returns.review', 'returns.approve', 'returns.reject')
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
  );

INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id, p.Id, @now, NULL
FROM Roles r CROSS JOIN Permissions p
WHERE r.Name = 'Finance' AND p.Name = 'returns.refund'
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
  );

DELETE rp
FROM RolePermissions rp
INNER JOIN Roles r ON r.Id = rp.RoleId
INNER JOIN Permissions p ON p.Id = rp.PermissionId
WHERE r.Name = 'Admin' AND p.Name = 'returns.refund';
""");
```

At the start of `Down`, restore the old Admin mapping, remove the CustomerSupport approval mapping, and remove the Finance refund mapping. Retain both roles and the base CustomerSupport view/review/reject permissions so rollback cannot delete assigned operational roles:

```csharp
migrationBuilder.Sql("""
DECLARE @now datetime2 = SYSUTCDATETIME();

INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id, p.Id, @now, NULL
FROM Roles r CROSS JOIN Permissions p
WHERE r.Name = 'Admin' AND p.Name = 'returns.refund'
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
  );

DELETE rp
FROM RolePermissions rp
INNER JOIN Roles r ON r.Id = rp.RoleId
INNER JOIN Permissions p ON p.Id = rp.PermissionId
WHERE (r.Name = 'CustomerSupport' AND p.Name = 'returns.approve')
   OR (r.Name = 'Finance' AND p.Name = 'returns.refund');
""");
```

- [ ] **Step 6: Inspect migration SQL**

Run:

```bash
dotnet ef migrations script AddTransactionalOutbox AddRefundDestination \
  --project Fruitables.csproj \
  --startup-project Fruitables.csproj \
  --idempotent \
  --output /tmp/add-refund-destination.sql
```

Expected: only the five nullable columns and the reviewed RBAC statements affect return-related data. No table is dropped and no existing refund is updated.

- [ ] **Step 7: Run the model test**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Models/Returns/Refund.cs Models/Returns/ReturnEnums.cs \
  Helpers/ReturnDisplay.cs Data/ApplicationDbContext.cs \
  Tests/ReturnModuleTests.cs Migrations
git commit -m "feat(returns): add refund destination schema" \
  -m "Manual bank refunds need protected destination fields and separate CSKH and Finance permissions."
```

---

### Task 2: Create one aggregate refund from the CSKH decision

**Files:**
- Modify: `ViewModels/Returns/ReturnRequestViewModels.cs:15-37`
- Modify: `Services/Returns/ReturnService.cs:41-121,141-166`
- Modify: `Tests/ReturnModuleTests.cs`
- Modify: `Tests/ReturnSqlServerIntegrationTests.cs:41-56`

**Interfaces:**
- Consumes: destination-ready `Refund` model from Task 1 and existing `IOutboxService`.
- Produces: `ReturnService.DecideAsync` creates one aggregate refund with key `return:{ReturnRequestId}:aggregate-refund`; customer submission stores `RequestedResolution.None`.

- [ ] **Step 1: Replace the decision regression test with the target behavior**

Update the partial decision test in `Tests/ReturnModuleTests.cs` so the input has no resolution and the assertions prove atomic aggregate creation:

```csharp
[Fact]
public async Task Decision_PartialApproval_CreatesOneAggregateRefund()
{
    await using var db = CreateContext();
    var graph = SeedOrder(db, quantity: 3, itemTotal: 90m);
    db.ReturnPolicies.Add(Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1)));
    await db.SaveChangesAsync();
    var clock = new MutableTimeProvider(graph.Order.DeliveredAtUtc!.Value.AddHours(1));
    var service = Returns(db, clock);
    var submitted = (await service.SubmitAsync(graph.Customer.Id, Submit(graph, "partial", 3))).Request!;
    await service.StartReviewAsync(submitted.Id, graph.Admin.Id, Array.Empty<byte>());

    var decision = await service.DecideAsync(graph.Admin.Id, new ReturnDecisionViewModel
    {
        ReturnRequestId = submitted.Id,
        Reason = "Một sản phẩm còn sử dụng được",
        Items =
        {
            new ReturnDecisionItemViewModel
            {
                ReturnRequestItemId = submitted.Items.Single().Id,
                ApprovedQuantity = 2
            }
        }
    });

    Assert.True(decision.Success);
    Assert.Equal(ReturnRequestStatus.ResolutionPending, decision.Request!.Status);
    Assert.Equal(ReturnResolutionType.PartialRefund, decision.Request.Resolution);
    var refund = Assert.Single(await db.Refunds.Where(x => x.ReturnRequestId == submitted.Id).ToListAsync());
    Assert.Null(refund.ReturnRequestItemId);
    Assert.Equal(60m, refund.Amount);
    Assert.Equal(RefundStatus.AwaitingDestination, refund.Status);
    Assert.Equal($"return:{submitted.Id}:aggregate-refund", refund.IdempotencyKey);
}
```

Add this assertion to `Submit_IsIdempotent_AndUsesRemainingQuantity`:

```csharp
Assert.All(first.Request.Items, item => Assert.Equal(ReturnResolutionType.None, item.RequestedResolution));
```

- [ ] **Step 2: Run the focused tests and confirm failure**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests.Decision_PartialApproval_CreatesOneAggregateRefund|FullyQualifiedName~ReturnModuleTests.Submit_IsIdempotent"
```

Expected: FAIL because the old service leaves the request approved, requires resolution input, and creates no aggregate refund.

- [ ] **Step 3: Remove resolution fields from customer and decision input**

Delete these properties:

```csharp
public ReturnResolutionType RequestedResolution { get; set; }
public ReturnResolutionType Resolution { get; set; }
```

Remove assignments to those properties from test builders. In `SubmitAsync`:

- Remove the `ResolutionAllowed` check and helper.
- Reject policies where both `AllowPartialRefund` and `AllowFullRefund` are false.
- Set `ReturnRequestItem.RequestedResolution = ReturnResolutionType.None`.

Use this policy guard:

```csharp
if (!check.Policy.AllowPartialRefund && !check.Policy.AllowFullRefund)
    return ReturnResult.Fail("Lý do này không hỗ trợ hoàn tiền.");
```

- [ ] **Step 4: Create the aggregate refund in `DecideAsync`**

After recalculating approved quantities and the shipping-fee rule, derive the aggregate amount and remaining order cap:

```csharp
var aggregateAmount = request.Items.Sum(x => x.ApprovedAmount)
    + (request.ShippingFeeApproved ? request.Order.ShippingFee : 0m);
var succeededForOrder = await _db.Refunds
    .Where(x => x.OrderId == request.OrderId && x.Status == RefundStatus.Succeeded)
    .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
var remainingOrderAmount = Math.Max(0m, request.Order.Total - succeededForOrder);
if (aggregateAmount > remainingOrderAmount)
    return ReturnResult.Fail("Số tiền hoàn vượt số tiền còn có thể hoàn của đơn hàng.");
```

For rejection, keep the existing `Rejected` transition and create no refund. For an approval, set the business resolution and add one refund:

```csharp
request.Resolution = aggregateAmount == remainingOrderAmount
    ? ReturnResolutionType.FullRefund
    : ReturnResolutionType.PartialRefund;

var decisionStatus = request.Items.All(x => x.ApprovedQuantity == x.RequestedQuantity)
    ? ReturnRequestStatus.Approved
    : ReturnRequestStatus.PartiallyApproved;
var refundKey = $"return:{request.Id}:aggregate-refund";
var refund = await _db.Refunds.SingleOrDefaultAsync(
    x => x.IdempotencyKey == refundKey,
    cancellationToken);
if (refund == null)
{
    refund = new Refund
    {
        ReturnRequestId = request.Id,
        ReturnRequestItemId = null,
        OrderId = request.OrderId,
        Amount = aggregateAmount,
        Method = RefundMethod.ManualBankTransfer,
        Status = RefundStatus.AwaitingDestination,
        IdempotencyKey = refundKey,
        CreatedByUserId = adminId,
        CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
    };
    _db.Refunds.Add(refund);
}
```

Record two audit transitions in the same `SaveChangesAsync` call:

1. `UnderReview` to `Approved` or `PartiallyApproved` with the decision event.
2. The decision state to `ResolutionPending` with `RefundCreated`.

Enqueue the existing decision status message and `OutboxMessageTypes.RefundCreated` before that save. Catch `DbUpdateConcurrencyException` exactly as the current decision path does. Do not call `SaveChangesAsync` between the decision and refund creation.

- [ ] **Step 5: Strengthen the SQL Server concurrency test**

After asserting that only one concurrent decision succeeds, add:

```csharp
await using var verify = new ApplicationDbContext(database.Options);
Assert.Single(await verify.Refunds
    .Where(x => x.ReturnRequestId == snapshot.Id && x.ReturnRequestItemId == null)
    .ToListAsync());
```

- [ ] **Step 6: Run return tests**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests"
```

Expected: PASS.

If `FRUITABLES_TEST_SQLSERVER` is configured, also run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnSqlServerIntegrationTests.ConcurrentAdminDecisionsProduceOneConcurrencyConflict"
```

Expected: PASS with one decision and one aggregate refund.

- [ ] **Step 7: Commit**

```bash
git add ViewModels/Returns/ReturnRequestViewModels.cs \
  Services/Returns/ReturnService.cs Tests/ReturnModuleTests.cs \
  Tests/ReturnSqlServerIntegrationTests.cs
git commit -m "refactor(returns): create aggregate refunds"
```

---

### Task 3: Protect destination data and implement the Finance lifecycle

**Files:**
- Create: `ViewModels/Returns/RefundViewModels.cs`
- Modify: `Services/Interfaces/IRefundService.cs`
- Modify: `Services/Returns/RefundService.cs`
- Modify: `Tests/ReturnModuleTests.cs`

**Interfaces:**
- Consumes: aggregate refund from Task 2, `IDataProtectionProvider`, `TimeProvider`, EF Core, and existing outbox.
- Produces:
  - `SaveDestinationAsync(int refundId, int customerId, RefundDestinationInputViewModel model, CancellationToken)`
  - `GetQueueAsync(RefundQueueFilter filter, CancellationToken)`
  - `GetFinanceTaskAsync(int refundId, int financeUserId, CancellationToken)`
  - `StartProcessingAsync(int refundId, int financeUserId, CancellationToken)`
  - `FailAsync(int refundId, int financeUserId, RefundFailureInputViewModel model, CancellationToken)`
  - Existing `ConfirmManualAsync` tightened to require `Processing` and clear protected PII.

- [ ] **Step 1: Add focused failing lifecycle tests**

Add a helper to `Tests/ReturnModuleTests.cs`:

```csharp
private static RefundService Refunds(ApplicationDbContext db, TimeProvider clock) =>
    new(db, clock, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
```

Add a test that starts from an approved request with an aggregate refund:

```csharp
[Fact]
public async Task Destination_IsProtected_LockedDuringProcessing_AndClearedOnSuccess()
{
    await using var db = CreateContext();
    var graph = SeedOrder(db, itemTotal: 100m);
    var finance = new User
    {
        Name = "Finance",
        Email = $"f{Guid.NewGuid():N}@test.local",
        Password = "hash",
        Role = UserRole.Admin
    };
    var policy = Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1));
    db.AddRange(policy, finance);
    var request = ApprovedRequest(graph, policy, 1, 100m);
    request.Status = ReturnRequestStatus.ResolutionPending;
    db.ReturnRequests.Add(request);
    await db.SaveChangesAsync();
    var refund = new Refund
    {
        ReturnRequestId = request.Id,
        OrderId = graph.Order.Id,
        Amount = 100m,
        Method = RefundMethod.ManualBankTransfer,
        Status = RefundStatus.AwaitingDestination,
        IdempotencyKey = $"return:{request.Id}:aggregate-refund",
        CreatedByUserId = graph.Admin.Id,
        CreatedAtUtc = Utc(2026, 7, 27)
    };
    db.Refunds.Add(refund);
    await db.SaveChangesAsync();
    var service = Refunds(db, new MutableTimeProvider(Utc(2026, 7, 27)));

    var saved = await service.SaveDestinationAsync(refund.Id, graph.Customer.Id, new RefundDestinationInputViewModel
    {
        RefundId = refund.Id,
        BankCode = "VCB",
        AccountNumber = "0123456789",
        AccountHolder = "NGUYEN VAN A"
    });

    Assert.True(saved.Success);
    Assert.NotEqual("0123456789", refund.DestinationAccountNumberProtected);
    Assert.Equal("6789", refund.DestinationAccountLast4);
    Assert.Equal(RefundStatus.AwaitingApproval, refund.Status);

    var viewed = await service.GetFinanceTaskAsync(refund.Id, finance.Id);
    Assert.True(viewed.Success);
    Assert.Equal("0123456789", viewed.Data!.AccountNumber);
    Assert.Equal("NGUYEN VAN A", viewed.Data.AccountHolder);

    Assert.True((await service.StartProcessingAsync(refund.Id, finance.Id)).Success);
    Assert.False((await service.SaveDestinationAsync(refund.Id, graph.Customer.Id, new RefundDestinationInputViewModel
    {
        RefundId = refund.Id,
        BankCode = "VCB",
        AccountNumber = "9999999999",
        AccountHolder = "NGUYEN VAN A"
    })).Success);

    Assert.True((await service.ConfirmManualAsync(refund.Id, "BANK-001", "proof.jpg", finance.Id)).Success);
    Assert.Null(refund.DestinationAccountNumberProtected);
    Assert.Null(refund.DestinationAccountHolderProtected);
    Assert.Equal("6789", refund.DestinationAccountLast4);
    Assert.Equal(RefundStatus.Succeeded, refund.Status);
}
```

Add a separate ownership test:

```csharp
[Fact]
public async Task Destination_RejectsAnotherCustomer()
{
    await using var db = CreateContext();
    var graph = SeedOrder(db);
    var policy = Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1));
    db.ReturnPolicies.Add(policy);
    var request = ApprovedRequest(graph, policy, 1, 100m);
    request.Status = ReturnRequestStatus.ResolutionPending;
    db.ReturnRequests.Add(request);
    await db.SaveChangesAsync();
    var refund = new Refund
    {
        ReturnRequestId = request.Id,
        OrderId = graph.Order.Id,
        Amount = 100m,
        Method = RefundMethod.ManualBankTransfer,
        Status = RefundStatus.AwaitingDestination,
        IdempotencyKey = $"return:{request.Id}:aggregate-refund",
        CreatedByUserId = graph.Admin.Id,
        CreatedAtUtc = Utc(2026, 7, 27)
    };
    db.Refunds.Add(refund);
    await db.SaveChangesAsync();

    var result = await Refunds(db, new MutableTimeProvider(Utc(2026, 7, 27)))
        .SaveDestinationAsync(refund.Id, 999, new RefundDestinationInputViewModel
        {
            RefundId = refund.Id,
            BankCode = "VCB",
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A"
        });

    Assert.False(result.Success);
}
```

- [ ] **Step 2: Run the tests and confirm compile failure**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests.Destination_"
```

Expected: FAIL because destination view models and service methods do not exist.

- [ ] **Step 3: Create refund UI models**

Create `ViewModels/Returns/RefundViewModels.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using Fruitables.Models.Returns;

namespace Fruitables.ViewModels.Returns;

public sealed class RefundDestinationInputViewModel
{
    public int RefundId { get; set; }
    public int ReturnRequestId { get; set; }

    [Required, StringLength(50, MinimumLength = 2)]
    public string BankCode { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9A-Za-z]{6,34}$")]
    public string AccountNumber { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string AccountHolder { get; set; } = string.Empty;
}

public enum RefundQueueBucket
{
    WaitingCustomer,
    Ready,
    Working,
    Completed
}

public sealed class RefundQueueFilter
{
    public RefundQueueBucket Bucket { get; set; } = RefundQueueBucket.Ready;
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class RefundQueueItemViewModel
{
    public int RefundId { get; init; }
    public int ReturnRequestId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public RefundStatus Status { get; init; }
    public string? BankCode { get; init; }
    public string? AccountLast4 { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class FinanceRefundViewModel
{
    public int RefundId { get; init; }
    public int ReturnRequestId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public RefundStatus Status { get; init; }
    public string? BankCode { get; init; }
    public string? AccountNumber { get; init; }
    public string? AccountHolder { get; init; }
    public string? AccountLast4 { get; init; }
}

public sealed class RefundFailureInputViewModel
{
    public int RefundId { get; set; }
    public bool RequestCustomerCorrection { get; set; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Replace the refund service interface**

Use this exact contract in `IRefundService`:

```csharp
using Fruitables.ViewModels.Returns;

namespace Fruitables.Services.Interfaces;

public interface IRefundService
{
    Task<(bool Success, string? Error)> SaveDestinationAsync(
        int refundId,
        int customerId,
        RefundDestinationInputViewModel model,
        CancellationToken cancellationToken = default);

    Task<List<RefundQueueItemViewModel>> GetQueueAsync(
        RefundQueueFilter filter,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, FinanceRefundViewModel? Data)> GetFinanceTaskAsync(
        int refundId,
        int financeUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> StartProcessingAsync(
        int refundId,
        int financeUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> FailAsync(
        int refundId,
        int financeUserId,
        RefundFailureInputViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> ConfirmManualAsync(
        int refundId,
        string transactionReference,
        string transferEvidenceStorageKey,
        int financeUserId,
        CancellationToken cancellationToken = default);
}
```

Delete the public `CreateAsync` method. Task 2 made `ReturnService.DecideAsync` the only creator for new aggregate refunds.

- [ ] **Step 5: Inject Data Protection and implement destination save/read**

Change the `RefundService` constructor to:

```csharp
private const string DestinationPurpose = "Fruitables.Returns.RefundDestination.v1";
private readonly IDataProtector _destinationProtector;

public RefundService(
    ApplicationDbContext db,
    TimeProvider clock,
    IDataProtectionProvider dataProtection,
    IOutboxService? outbox = null)
{
    _db = db;
    _clock = clock;
    _destinationProtector = dataProtection.CreateProtector(DestinationPurpose);
    _outbox = outbox ?? new OutboxService(db, clock);
}
```

`SaveDestinationAsync` must validate the input again at the service boundary before querying the refund:

```csharp
var validationResults = new List<ValidationResult>();
if (!Validator.TryValidateObject(
        model,
        new ValidationContext(model),
        validationResults,
        validateAllProperties: true))
    return (false, "Thông tin nhận tiền không hợp lệ.");
```

Then it must:

1. Trim and uppercase bank code and account holder.
2. Remove spaces from account number.
3. Select only an aggregate refund owned by `customerId` with status `AwaitingDestination` or `AwaitingApproval`.
4. Protect account number and holder before assigning entity properties.
5. Save `DestinationAccountLast4`, `DestinationSubmittedAtUtc`, and status `AwaitingApproval`.
6. Add `RefundDestinationSubmitted` without putting bank data in the event note.
7. Catch concurrent status changes and return a generic retry message.

`GetFinanceTaskAsync` must:

1. Load the aggregate refund with return request, order, and customer.
2. Return masked data without decryption when status is `AwaitingDestination`.
3. Unprotect number and holder only for `AwaitingApproval`, `Processing`, or `Failed`.
4. Add `RefundDestinationViewed` with no destination values in the note.
5. Return `"Không thể đọc thông tin nhận tiền."` on `CryptographicException`.

- [ ] **Step 6: Implement conditional claim, failure, retry, and success**

`StartProcessingAsync` must use a transaction and `ExecuteUpdateAsync` with this state guard:

```csharp
x => x.Id == refundId
    && x.ReturnRequestItemId == null
    && (x.Status == RefundStatus.AwaitingApproval || x.Status == RefundStatus.Failed)
    && x.DestinationAccountNumberProtected != null
    && x.DestinationAccountHolderProtected != null
```

Set `Status = Processing`, `ProcessedByUserId = financeUserId`, and add `RefundProcessingStarted`. Reject the claim when `Amount >= 500_000m && CreatedByUserId == financeUserId`.

`FailAsync` must require `Processing` and the same `ProcessedByUserId`:

- Retry path: set refund to `Failed`, request to `ResolutionFailed`, preserve protected destination, and add `RefundFailed`.
- Correction path: set refund to `AwaitingDestination`, request to `ResolutionPending`, set `DestinationBankCode`, both protected fields, `DestinationAccountLast4`, and `DestinationSubmittedAtUtc` to null, clear `ProcessedByUserId`, store the internal failure reason, and add `RefundDestinationCorrectionRequested`.

`ConfirmManualAsync` must require:

```csharp
refund.Status == RefundStatus.Processing
refund.ProcessedByUserId == financeUserId
refund.DestinationAccountNumberProtected != null
refund.DestinationAccountHolderProtected != null
```

Keep the unique transaction-reference and 500.000 đồng maker-checker checks. On success:

```csharp
refund.Status = RefundStatus.Succeeded;
refund.TransactionReference = transactionReference.Trim();
refund.TransferEvidenceStorageKey = Path.GetFileName(transferEvidenceStorageKey);
refund.ProcessedAtUtc = now;
refund.DestinationAccountNumberProtected = null;
refund.DestinationAccountHolderProtected = null;
```

Update `Order.PaymentStatus` from all succeeded order refunds and set the request to `Resolved` because the aggregate refund already represents its approved total. Keep order status unchanged.

- [ ] **Step 7: Implement Finance queue projection**

`GetQueueAsync` must use `AsNoTracking()` and map buckets as follows:

```csharp
RefundQueueBucket.WaitingCustomer => x.Status == RefundStatus.AwaitingDestination,
RefundQueueBucket.Ready => x.Status == RefundStatus.AwaitingApproval,
RefundQueueBucket.Working => x.Status == RefundStatus.Processing || x.Status == RefundStatus.Failed,
RefundQueueBucket.Completed => x.Status == RefundStatus.Succeeded || x.Status == RefundStatus.Cancelled
```

Project directly to `RefundQueueItemViewModel`; do not decrypt destination data in the list query. Apply search to return number, order number, or customer email. Clamp page size to 1 through 100.

- [ ] **Step 8: Update existing refund tests and run the focused suite**

Replace direct `new RefundService(db, clock)` calls with the `Refunds` helper. Update `ManualRefund_UpdatesProjectionWithoutChangingOrderStatus` to seed `Processing`, protected destination, and `ProcessedByUserId` before confirmation.

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests.Destination_|FullyQualifiedName~ReturnModuleTests.ManualRefund"
```

Expected: PASS. Inspect assertion output to confirm ciphertext differs from plaintext and becomes null after success.

- [ ] **Step 9: Commit**

```bash
git add ViewModels/Returns/RefundViewModels.cs \
  Services/Interfaces/IRefundService.cs Services/Returns/RefundService.cs \
  Tests/ReturnModuleTests.cs
git commit -m "feat(returns): secure manual refund flow"
```

---

### Task 4: Simplify the customer claim and tracking UI

**Files:**
- Modify: `Controllers/ReturnController.cs`
- Modify: `Helpers/ReturnDisplay.cs`
- Modify: `Views/Return/Create.cshtml`
- Modify: `Views/Return/Details.cshtml`
- Modify: `Views/Return/Index.cshtml`
- Modify: `Views/Return/_StatusTimeline.cshtml`
- Modify: `Tests/ReturnControllerSecurityTests.cs`
- Modify: `Tests/ReturnModuleTests.cs`

**Interfaces:**
- Consumes: `IRefundService.SaveDestinationAsync` from Task 3 and aggregate refund navigation loaded by `IReturnService.GetForCustomerAsync`.
- Produces: customer claim form without resolution choices; owner-only destination POST; five customer progress groups with outcome-specific terminal copy.

- [ ] **Step 1: Write customer progress and controller tests**

Add to `ReturnModuleTests.cs`:

```csharp
[Theory]
[InlineData(ReturnRequestStatus.Submitted, "Đã tiếp nhận")]
[InlineData(ReturnRequestStatus.AwaitingEvidence, "Cần bổ sung")]
[InlineData(ReturnRequestStatus.UnderReview, "Đang xem xét")]
[InlineData(ReturnRequestStatus.ResolutionPending, "Đang hoàn tiền")]
[InlineData(ReturnRequestStatus.Resolved, "Đã hoàn tiền")]
[InlineData(ReturnRequestStatus.Rejected, "Đã từ chối")]
public void CustomerProgress_UsesBusinessCopy(ReturnRequestStatus status, string expected)
{
    Assert.Equal(expected, ReturnDisplay.CustomerProgress(status));
}
```

Update `CustomerCannotReadAnotherUsersReturnRequest` constructor to include `Mock.Of<IRefundService>()`. Add a destination redirect test:

```csharp
[Fact]
public async Task CustomerDestinationPost_UsesAuthenticatedUserId()
{
    var refunds = new Mock<IRefundService>();
    refunds.Setup(x => x.SaveDestinationAsync(
            7,
            10,
            It.IsAny<RefundDestinationInputViewModel>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync((true, (string?)null));
    await using var db = new ApplicationDbContext(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    var controller = new ReturnController(
        Mock.Of<IReturnService>(),
        Mock.Of<IReturnEligibilityService>(),
        Mock.Of<IReturnEvidenceService>(),
        refunds.Object,
        db)
    {
        ControllerContext = Context(10, "Customer")
    };

    var result = await controller.SaveRefundDestination(new RefundDestinationInputViewModel
    {
        RefundId = 7,
        ReturnRequestId = 42,
        BankCode = "VCB",
        AccountNumber = "0123456789",
        AccountHolder = "NGUYEN VAN A"
    });

    refunds.Verify(x => x.SaveDestinationAsync(
        7,
        10,
        It.IsAny<RefundDestinationInputViewModel>(),
        It.IsAny<CancellationToken>()));
    Assert.IsType<RedirectToActionResult>(result);
}
```

- [ ] **Step 2: Run the focused tests and confirm failure**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests.CustomerProgress|FullyQualifiedName~ReturnControllerSecurityTests.CustomerDestinationPost"
```

Expected: FAIL because the helper, constructor dependency, and action do not exist.

- [ ] **Step 3: Add customer progress labels**

Add to `ReturnDisplay`:

```csharp
public static string CustomerProgress(ReturnRequestStatus status) => status switch
{
    ReturnRequestStatus.Submitted => "Đã tiếp nhận",
    ReturnRequestStatus.AwaitingEvidence => "Cần bổ sung",
    ReturnRequestStatus.UnderReview => "Đang xem xét",
    ReturnRequestStatus.Approved or
    ReturnRequestStatus.PartiallyApproved or
    ReturnRequestStatus.ResolutionPending or
    ReturnRequestStatus.ResolutionFailed => "Đang hoàn tiền",
    ReturnRequestStatus.Resolved => "Đã hoàn tiền",
    ReturnRequestStatus.Rejected => "Đã từ chối",
    ReturnRequestStatus.Cancelled => "Đã hủy",
    ReturnRequestStatus.Expired => "Đã quá hạn",
    _ => Text(status)
};
```

- [ ] **Step 4: Add destination POST to the customer controller**

Inject `IRefundService` and add:

```csharp
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> SaveRefundDestination(RefundDestinationInputViewModel model)
{
    if (!ModelState.IsValid)
    {
        TempData["ErrorMessage"] = "Thông tin nhận tiền không hợp lệ.";
        return RedirectToAction(nameof(Details), new { id = model.ReturnRequestId });
    }

    var result = await _refunds.SaveDestinationAsync(model.RefundId, UserId, model);
    TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
        ? "Đã lưu thông tin nhận tiền."
        : result.Error;
    return RedirectToAction(nameof(Details), new { id = model.ReturnRequestId });
}
```

Render `ReturnRequestId` as a hidden field. A forged value can only alter the redirect target; `SaveDestinationAsync` authorizes with `refundId`, the authenticated `UserId`, and the refund's real `ReturnRequest.UserId`.

- [ ] **Step 5: Simplify `Views/Return/Create.cshtml`**

Remove the requested-resolution `<select>`. For each item card:

- Disable quantity, reason, and description until its checkbox is selected.
- Set `required` only while selected.
- Keep evidence requirement derived from selected reasons.
- Keep the existing idempotency key and submit lock.

Use one JS function that toggles both disabled and required state:

```javascript
const refreshItem = card => {
    const selected = card.querySelector('.item-toggle').checked;
    card.querySelectorAll('[data-item-field]').forEach(field => {
        field.disabled = !selected;
        field.required = selected && field.dataset.optional !== 'true';
    });
};
```

Add `data-item-field` to quantity, reason, and description. Keep unselected fields disabled so model binding cannot send stale item data.

- [ ] **Step 6: Make customer details action-first**

At the top of `Views/Return/Details.cshtml`:

- Display `ReturnDisplay.CustomerProgress(Model.Status)`.
- Find the aggregate refund with `Model.Refunds.SingleOrDefault(x => x.ReturnRequestItemId == null)`.
- Show evidence upload only for `AwaitingEvidence`.
- Show destination form only when aggregate refund status is `AwaitingDestination` or `AwaitingApproval`.
- Disable destination form when status is `Processing`.
- Render only `DestinationBankCode` and `DestinationAccountLast4` after save.
- Keep approved amount, customer-safe decision reason, and timeline.

Use native inputs and a `datalist` for common bank codes. The input must still accept another 2 through 50 character bank code; do not add a bank package.

Update `Views/Return/Index.cshtml` to use `CustomerProgress`. In the customer timeline, keep only public events and omit `RefundDestinationViewed` because it is an internal audit event.

- [ ] **Step 7: Run customer tests**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnControllerSecurityTests|FullyQualifiedName~ReturnModuleTests.CustomerProgress|FullyQualifiedName~ReturnModuleTests.Destination_"
```

Expected: PASS. The reflection test must include `SaveRefundDestination` and confirm it has antiforgery.

- [ ] **Step 8: Commit**

```bash
git add Controllers/ReturnController.cs Helpers/ReturnDisplay.cs \
  Views/Return Tests/ReturnControllerSecurityTests.cs Tests/ReturnModuleTests.cs
git commit -m "refactor(returns): simplify customer claim UI"
```

---

### Task 5: Focus the admin return UI on CSKH review

**Files:**
- Modify: `ViewModels/Returns/ReturnRequestViewModels.cs`
- Modify: `Services/Returns/ReturnService.cs:127-136`
- Modify: `Areas/Admin/Controllers/ReturnController.cs`
- Modify: `Areas/Admin/Views/Return/Index.cshtml`
- Modify: `Areas/Admin/Views/Return/_ReturnQueue.cshtml`
- Modify: `Areas/Admin/Views/Return/Detail.cshtml`
- Modify: `Areas/Admin/Views/Return/_DecisionForm.cshtml`
- Modify: `Tests/ReturnModuleTests.cs`
- Modify: `Tests/ReturnControllerSecurityTests.cs`

**Interfaces:**
- Consumes: aggregate decision behavior from Task 2 and RBAC mapping from Task 1.
- Produces: `ReturnQueueBucket` tabs and a CSKH-only detail screen with no Finance, disposition, resolution, amount, or technical state controls.

- [ ] **Step 1: Write a failing queue-bucket test**

Add to `ReturnModuleTests.cs`:

```csharp
[Fact]
public async Task ReturnQueueBucket_SeparatesActionableCskhWork()
{
    await using var db = CreateContext();
    var graph = SeedOrder(db);
    var policy = Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1));
    db.ReturnPolicies.Add(policy);
    var submitted = ApprovedRequest(graph, policy, 1, 100m);
    submitted.Status = ReturnRequestStatus.Submitted;
    submitted.ReturnNumber = "RT-SUBMITTED";
    var waiting = ApprovedRequest(graph, policy, 1, 100m);
    waiting.Status = ReturnRequestStatus.AwaitingEvidence;
    waiting.ReturnNumber = "RT-WAITING";
    db.ReturnRequests.AddRange(submitted, waiting);
    await db.SaveChangesAsync();

    var service = Returns(db, new MutableTimeProvider(Utc(2026, 7, 27)));
    var intake = await service.GetQueueAsync(new ReturnQueueFilter
    {
        Bucket = ReturnQueueBucket.Intake
    });

    Assert.Contains(intake, x => x.ReturnNumber == "RT-SUBMITTED");
    Assert.DoesNotContain(intake, x => x.ReturnNumber == "RT-WAITING");
}
```

- [ ] **Step 2: Run the test and confirm compile failure**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests.ReturnQueueBucket_SeparatesActionableCskhWork"
```

Expected: FAIL because `ReturnQueueBucket` and `Bucket` do not exist.

- [ ] **Step 3: Add CSKH queue buckets**

Add:

```csharp
public enum ReturnQueueBucket
{
    Intake,
    WaitingCustomer,
    Reviewing,
    Completed
}
```

Add nullable `Bucket` to `ReturnQueueFilter`. In `GetQueueAsync`, apply:

```csharp
query = filter.Bucket switch
{
    ReturnQueueBucket.Intake => query.Where(x => x.Status == ReturnRequestStatus.Submitted),
    ReturnQueueBucket.WaitingCustomer => query.Where(x => x.Status == ReturnRequestStatus.AwaitingEvidence),
    ReturnQueueBucket.Reviewing => query.Where(x => x.Status == ReturnRequestStatus.UnderReview),
    ReturnQueueBucket.Completed => query.Where(x =>
        x.Status == ReturnRequestStatus.Rejected ||
        x.Status == ReturnRequestStatus.ResolutionPending ||
        x.Status == ReturnRequestStatus.ResolutionFailed ||
        x.Status == ReturnRequestStatus.Resolved ||
        x.Status == ReturnRequestStatus.Cancelled ||
        x.Status == ReturnRequestStatus.Expired),
    _ => query
};
```

Keep search, reason, date, pagination, and SLA ordering.

- [ ] **Step 4: Remove non-CSKH actions from the return controller**

Remove these dependencies from `Areas/Admin/Controllers/ReturnController`:

- `IRefundService`.
- `IReturnEvidenceService`.
- `IReturnDispositionService`.

Remove these actions:

- `CreateRefund`.
- `ConfirmRefund`.
- `UpdateResolution`.
- `RecordDisposition`.

Keep `IRbacService` for the decision permission check. Keep all CSKH POST actions protected by antiforgery and `returns.review`, `returns.approve`, or `returns.reject`.

- [ ] **Step 5: Replace technical filters and mixed forms in Razor**

In `Index.cshtml`:

- Add tabs for `Intake`, `WaitingCustomer`, `Reviewing`, and `Completed`.
- Remove the raw `ReturnRequestStatus` dropdown.
- Keep search, reason, and date filters.

In `Detail.cshtml`:

- Keep summary, items, evidence, decision reason, and timeline.
- Show only destination bank code and last4 after the customer submits them; never show protected fields.
- Keep start-review and request-evidence forms.
- Keep `_DecisionForm` only for `UnderReview`.
- Remove refund creation, refund confirmation, disposition, and resolution-state forms.
- Add a read-only banner after approval: `Đã chuyển sang hàng đợi Finance`.

In `_DecisionForm.cshtml`:

- Remove the resolution `<select>`.
- Keep approved quantity.
- Show the server-calculated cap as read-only text.
- Keep merchant-fault and shipping-fee controls.
- Label the submit button `Duyệt và chuyển Finance` when any quantity is positive.

- [ ] **Step 6: Tighten controller security tests**

Update constructor mocks after dependency removal. Add assertions that:

```csharp
Assert.Null(typeof(AdminReturnController).GetMethod("CreateRefund"));
Assert.Null(typeof(AdminReturnController).GetMethod("ConfirmRefund"));
Assert.Null(typeof(AdminReturnController).GetMethod("RecordDisposition"));
```

Keep the existing role, permission, and antiforgery reflection assertions.

- [ ] **Step 7: Run CSKH tests**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnModuleTests.ReturnQueueBucket|FullyQualifiedName~ReturnControllerSecurityTests"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add ViewModels/Returns/ReturnRequestViewModels.cs \
  Services/Returns/ReturnService.cs Areas/Admin/Controllers/ReturnController.cs \
  Areas/Admin/Views/Return Tests/ReturnModuleTests.cs \
  Tests/ReturnControllerSecurityTests.cs
git commit -m "refactor(returns): focus CSKH review UI"
```

---

### Task 6: Add the Finance refund queue and task screen

**Files:**
- Create: `Areas/Admin/Controllers/RefundController.cs`
- Create: `Areas/Admin/Views/Refund/Index.cshtml`
- Create: `Areas/Admin/Views/Refund/Detail.cshtml`
- Modify: `Areas/Admin/Views/Shared/_AdminSidebar.cshtml:88-97`
- Modify: `wwwroot/css/return-admin.css`
- Modify: `Tests/ReturnControllerSecurityTests.cs`

**Interfaces:**
- Consumes: all `IRefundService` methods from Task 3 and `IReturnEvidenceService.UploadAsync` for transfer proof.
- Produces: permission-gated Finance queue and detail routes with no editable amount.

- [ ] **Step 1: Write failing controller metadata tests**

Alias the new controller in `ReturnControllerSecurityTests.cs`:

```csharp
using AdminRefundController = Fruitables.Areas.Admin.Controllers.RefundController;
```

Add:

```csharp
[Fact]
public void FinanceRefundRoutes_RequireRefundPermissionAndAntiforgery()
{
    var controllerType = typeof(AdminRefundController);
    var authorize = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());
    Assert.Equal("Admin,SuperAdmin", authorize.Roles);

    var actions = controllerType
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(x => !x.IsSpecialName)
        .ToList();
    Assert.All(actions, action =>
        Assert.Contains(action.GetCustomAttributes<RequirePermissionAttribute>(),
            attribute => attribute.Permissions.Contains("returns.refund")));
    Assert.All(actions.Where(x => x.GetCustomAttribute<HttpPostAttribute>() != null),
        action => Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
}
```

- [ ] **Step 2: Run the test and confirm compile failure**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnControllerSecurityTests.FinanceRefundRoutes"
```

Expected: FAIL because `RefundController` does not exist.

- [ ] **Step 3: Create the Finance controller**

Create `Areas/Admin/Controllers/RefundController.cs` with this public surface:

```csharp
[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class RefundController : Controller
{
    [RequirePermission("returns.refund")]
    public Task<IActionResult> Index(RefundQueueFilter filter);

    [RequirePermission("returns.refund")]
    public Task<IActionResult> Detail(int id);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public Task<IActionResult> Start(int id);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public Task<IActionResult> Fail(RefundFailureInputViewModel model);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public Task<IActionResult> Confirm(
        int id,
        string transactionReference,
        IFormFile transferEvidence);
}
```

Implementation rules:

- `Index` calls `GetQueueAsync` and stores the filter in `ViewBag.Filter`.
- `Detail` calls `GetFinanceTaskAsync(id, AdminId)`; return `NotFound` on failure without exposing the reason.
- `Start` calls `StartProcessingAsync`.
- `Fail` calls `FailAsync`.
- `Confirm` first calls `GetFinanceTaskAsync` to obtain the authorized `ReturnRequestId`, then uploads proof as internal evidence, then calls `ConfirmManualAsync`.
- All POST actions redirect to `Detail` and use generic TempData messages.
- Never copy account number or account holder into TempData.

- [ ] **Step 4: Create Finance queue view**

`Areas/Admin/Views/Refund/Index.cshtml` must:

- Use the admin dashboard layout and `return-admin.css`.
- Render tabs for `WaitingCustomer`, `Ready`, `Working`, and `Completed`.
- Show return number, order number, customer, amount, status, bank code, and last4.
- Never render protected ciphertext or a full account number.
- Link rows to `Refund/Detail/{id}`.

- [ ] **Step 5: Create Finance detail view**

`Areas/Admin/Views/Refund/Detail.cshtml` must render actions by status:

- `AwaitingDestination`: read-only `Đang chờ khách cung cấp tài khoản`.
- `AwaitingApproval`: full destination plus `Bắt đầu xử lý`.
- `Processing`: fixed amount, full destination, transaction reference input, proof upload, confirmation button, and failure form.
- `Failed`: failure reason and retry button.
- `Succeeded`: masked destination, reference, proof link, and completion timestamp.

The amount must be text, never an `<input>`. The failure form contains `RequestCustomerCorrection` and a required reason. Escape all customer and note text through Razor's default encoding.

- [ ] **Step 6: Add permission-aware navigation**

In `_AdminSidebar.cshtml`:

- Keep the return link for `returns.view`.
- Add `Hoàn tiền` pointing to `Admin/Refund/Index` only for `returns.refund`.
- Mark links active by both controller and action so Return and Refund do not highlight together.

Reuse existing admin CSS. Add only missing tab, masked-account, fixed-amount, and action-card rules to `return-admin.css`.

- [ ] **Step 7: Run controller and refund tests**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnControllerSecurityTests|FullyQualifiedName~ReturnModuleTests.Destination_|FullyQualifiedName~ReturnModuleTests.ManualRefund"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Areas/Admin/Controllers/RefundController.cs \
  Areas/Admin/Views/Refund Areas/Admin/Views/Shared/_AdminSidebar.cshtml \
  wwwroot/css/return-admin.css Tests/ReturnControllerSecurityTests.cs
git commit -m "feat(returns): add finance refund queue"
```

---

### Task 7: Add rollout checks and verify the complete journey

**Files:**
- Create: `docs/returns/refund-ui-rollout.md`

**Interfaces:**
- Consumes: completed Customer, CSKH, and Finance journey.
- Produces: deploy gate for legacy refunds, migration rehearsal evidence, and verified release behavior.

- [ ] **Step 1: Write the rollout runbook**

Create `docs/returns/refund-ui-rollout.md` with these exact gates:

````markdown
# Refund UI rollout

## Before migration

1. Back up the target database.
2. Confirm the Data Protection key ring is persistent and access-restricted.
3. Run the nonterminal return report below.
4. Assign operational users to CustomerSupport or Finance RBAC roles. Each user must also retain the legacy Admin or SuperAdmin role required by the admin area.

## Nonterminal return report

Status values: Rejected=5, Resolved=8, Cancelled=9, Expired=10.

```sql
SELECT
    rr.Id,
    rr.ReturnNumber,
    rr.Status,
    COUNT(r.Id) AS RefundCount,
    SUM(CASE WHEN r.ReturnRequestItemId IS NOT NULL THEN 1 ELSE 0 END) AS ItemRefundCount,
    SUM(CASE WHEN r.ReturnRequestItemId IS NULL THEN 1 ELSE 0 END) AS AggregateRefundCount
FROM ReturnRequests rr
LEFT JOIN Refunds r ON r.ReturnRequestId = rr.Id
WHERE rr.Status NOT IN (5, 8, 9, 10)
GROUP BY rr.Id, rr.ReturnNumber, rr.Status
ORDER BY rr.Id;
```

Do not enable the new UI while a nonterminal request has an item-level refund. Resolve it through the old flow first. For an approved request with no refund, create the aggregate refund with the same rules and stable idempotency key used by `ReturnService.DecideAsync`, then review the resulting amount before enabling the UI.

## After migration

1. Verify CustomerSupport has returns.view, returns.review, returns.approve, and returns.reject.
2. Verify Finance has returns.refund.
3. Verify generic Admin no longer has returns.refund.
4. Verify SuperAdmin retains all return permissions.
5. Run the three-role smoke flow.
6. Keep evidence upload closed to public production until malware scanning or quarantine exists.

## Rollback

Disable the new UI before rolling back. Do not roll back while any refund contains destination data or uses AwaitingDestination, AwaitingApproval, or Processing. Finish or cancel those refunds first, export the audit report, then rehearse the Down migration on a restored test backup.
````

- [ ] **Step 2: Run the return and outbox suites**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Return|FullyQualifiedName~Outbox"
```

Expected: PASS with zero failed tests. Skipped SQL Server tests are acceptable only when `FRUITABLES_TEST_SQLSERVER` is absent.

- [ ] **Step 3: Run SQL Server integration tests when configured**

Run:

```bash
dotnet test Tests/Fruitables.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ReturnSqlServerIntegrationTests"
```

Expected with `FRUITABLES_TEST_SQLSERVER` configured: PASS, including one aggregate refund after concurrent decisions and duplicate-reference rejection.

- [ ] **Step 4: Rehearse migration and rollback on a disposable database**

Run:

```bash
dotnet ef migrations script AddTransactionalOutbox AddRefundDestination \
  --project Fruitables.csproj \
  --startup-project Fruitables.csproj \
  --idempotent \
  --output /tmp/add-refund-destination-final.sql
```

Apply the script to a restored test database, run the rollout SQL report, then run `dotnet ef database update AddTransactionalOutbox` against that disposable database. Expected: destination columns are removed, operational refund data has already been cleared, and RBAC rollback statements complete without deleting the Finance role.

- [ ] **Step 5: Run the release build**

Run:

```bash
dotnet build Fruitables.csproj --configuration Release --no-restore
```

Expected: exit code 0 with no build errors.

- [ ] **Step 6: Exercise the three-role flow with Playwright**

Using local accounts already mapped through the RBAC admin UI:

1. Customer opens a delivered order, selects one item, submits evidence, and sees `Đã tiếp nhận`.
2. CSKH opens `Cần tiếp nhận`, starts review, approves a partial quantity, and sees `Đã chuyển sang hàng đợi Finance`.
3. Customer opens the claim, submits bank destination, reloads, and sees only bank code plus last4.
4. CSKH reloads the claim and still sees only masked destination.
5. Finance opens `Sẵn sàng chuyển`, sees the fixed amount, starts processing, uploads proof, enters a unique reference, and confirms.
6. Customer reloads and sees `Đã hoàn tiền` with no editable destination form.
7. Query the refund row and confirm both protected full-value columns are null while last4 remains.

Also run one failure branch: Finance requests destination correction, Customer updates the account, and Finance can claim the task again.

Expected: no unauthorized full account display, no editable amount, no disposition or replacement/store-credit controls, and no browser console errors.

- [ ] **Step 7: Commit the runbook**

```bash
git add docs/returns/refund-ui-rollout.md
git commit -m "docs(returns): add refund rollout runbook"
```

- [ ] **Step 8: Inspect the final branch diff**

Run:

```bash
git status --short
git diff --check origin/master...HEAD
git log --oneline --decorate origin/master..HEAD
```

Expected: only files listed in this plan are changed, `git diff --check` reports no whitespace errors, and commits are split by the seven tasks above.
