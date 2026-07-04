using Fruitables.Data;
using Fruitables.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public class SePayWebhookControllerTests
{
    [Fact]
    public async Task SePayTransactionId_IsUnique()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        context.SePayTransactions.Add(new SePayTransaction
        {
            SePayTransactionId = 92704,
            PaymentCode = "FTB7K3P9Q2",
            TransferAmount = 45000,
            Status = SePayTransactionStatus.Paid,
            Payload = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        Assert.True(await context.SePayTransactions.AnyAsync(t => t.SePayTransactionId == 92704));
    }
}
