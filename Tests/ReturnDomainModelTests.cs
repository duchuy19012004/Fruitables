using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public sealed class ReturnDomainModelTests
{
    [Fact]
    public void FreshProduceFields_UseWholeKilogramsAndDamageTiers()
    {
        var item = new ReturnRequestItem
        {
            RequestedQuantity = 1,
            ApprovedQuantity = 1,
            DamagePercentageRequested = ReturnDamagePercentage.Fifty,
            DamagePercentageApproved = ReturnDamagePercentage.Fifty,
            Status = ReturnItemDecisionStatus.Approved
        };
        var disposition = new InventoryDisposition { Quantity = 1, QuantityKg = 0.5m };

        Assert.Equal(1, item.RequestedQuantity);
        Assert.Equal(ReturnDamagePercentage.Fifty, item.DamagePercentageApproved);
        Assert.Equal(0.5m, disposition.QuantityKg);
    }

    [Fact]
    public async Task EvidenceLinks_CannotDuplicateTheSameEvidenceAndItem()
    {
        await using var db = CreateContext();
        var graph = SeedGraph(db);
        db.ReturnEvidenceLinks.Add(new ReturnEvidenceLink
        {
            ReturnEvidenceId = graph.Evidence.Id,
            ReturnRequestItemId = graph.Item.Id
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        db.ReturnEvidenceLinks.Add(new ReturnEvidenceLink
        {
            ReturnEvidenceId = graph.Evidence.Id,
            ReturnRequestItemId = graph.Item.Id
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static ApplicationDbContext CreateContext() => new(TestDbContextFactory.CreateSqliteOptions());

    private static Graph SeedGraph(ApplicationDbContext db)
    {
        var customer = new User { Name = "Customer", Email = $"customer-{Guid.NewGuid():N}@test.local", Password = "hash" };
        var admin = new User { Name = "Admin", Email = $"admin-{Guid.NewGuid():N}@test.local", Password = "hash", Role = UserRole.Admin };
        var category = new Category { Name = "Fruit", Slug = $"fruit-{Guid.NewGuid():N}" };
        var product = new Product { Category = category, Name = "Apple", Slug = $"apple-{Guid.NewGuid():N}", Price = 10, StockQuantity = 10 };
        var order = new Order
        {
            User = customer,
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Paid,
            DeliveredAtUtc = DateTime.UtcNow,
            Subtotal = 10,
            Total = 10
        };
        var orderItem = new OrderItem
        {
            Order = order,
            Product = product,
            ProductName = product.Name,
            Quantity = 1,
            BasePrice = 10,
            Price = 10,
            Total = 10
        };
        var request = new ReturnRequest
        {
            ReturnNumber = $"RT{Guid.NewGuid():N}"[..20],
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Order = order,
            User = customer,
            Status = ReturnRequestStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
            ClaimDeadlineAtUtc = DateTime.UtcNow.AddHours(24),
            ReviewDueAtUtc = DateTime.UtcNow.AddHours(24)
        };
        var item = new ReturnRequestItem
        {
            ReturnRequest = request,
            OrderItem = orderItem,
            RequestedQuantity = 1,
            Reason = ReturnReasonCode.DamagedOrBruised,
            Description = "Quality issue",
            NetPaidAmountSnapshot = 10,
            RequestedAmount = 10,
            ClaimDeadlineAtUtcSnapshot = DateTime.UtcNow.AddHours(24)
        };
        var evidence = new ReturnEvidence
        {
            ReturnRequest = request,
            ReturnRequestItem = item,
            UploadedByUser = customer,
            OriginalFileName = "proof.png",
            StorageKey = $"{Guid.NewGuid():N}.png",
            MimeType = "image/png",
            SizeBytes = 8,
            Sha256Checksum = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow
        };

        request.Items.Add(item);
        request.Evidences.Add(evidence);
        db.AddRange(admin, request, evidence);
        db.SaveChanges();
        return new(item, evidence);
    }

    private sealed record Graph(ReturnRequestItem Item, ReturnEvidence Evidence);
}
