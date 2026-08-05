using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public sealed class ReturnServiceTests
{
    [Fact]
    public async Task ReturnRequest_order_id_is_unique()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);

        var customer = new User
        {
            Id = 10,
            Name = "Customer",
            Email = "customer@example.test",
            Password = "hash"
        };
        var order = new Order
        {
            Id = 1,
            User = customer,
            OrderNumber = "ORD-RETURN-UNIQUE",
            Status = OrderStatus.Delivered,
            DeliveredAtUtc = DateTime.UtcNow.AddHours(-1),
            PaymentStatus = PaymentStatus.Paid
        };
        context.AddRange(customer, order);
        await context.SaveChangesAsync();

        context.ReturnRequests.Add(new ReturnRequest
        {
            ReturnNumber = "RET-0001",
            OrderId = order.Id,
            UserId = customer.Id,
            Status = ReturnRequestStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
            ClaimDeadlineAtUtc = DateTime.UtcNow.AddHours(23)
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        context.ReturnRequests.Add(new ReturnRequest
        {
            ReturnNumber = "RET-0002",
            OrderId = order.Id,
            UserId = customer.Id,
            Status = ReturnRequestStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
            ClaimDeadlineAtUtc = DateTime.UtcNow.AddHours(23)
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
