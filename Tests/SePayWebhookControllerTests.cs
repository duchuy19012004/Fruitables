using System.Security.Cryptography;
using System.Text;
using Fruitables.Controllers.Api;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Fruitables.Services.Infrastructure;

namespace Fruitables.Tests;

public class SePayWebhookControllerTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private static SePayWebhookController CreateController(ApplicationDbContext context, string body, string secret = "test-secret")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SePayOptions
        {
            WebhookSecret = secret,
            PaymentCodePrefix = "FTB"
        });

        var controller = new SePayWebhookController(context, options, NullLogger<SePayWebhookController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.Headers["X-SePay-Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        httpContext.Request.Headers["X-SePay-Signature"] = Sign(secret, httpContext.Request.Headers["X-SePay-Timestamp"]!, body);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static string Sign(string secret, string timestamp, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public async Task Webhook_ValidPayload_MarksOrderPaid()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        context.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-1",
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentStatus = PaymentStatus.Pending,
            PaymentCode = "FTB7K3P9Q2",
            Total = 45000
        });
        await context.SaveChangesAsync();

        const string body = """
        {"id":92704,"gateway":"Vietcombank","transactionDate":"2024-07-02 11:08:33","accountNumber":"1017588888","subAccount":"","code":"FTB7K3P9Q2","content":"FTB7K3P9Q2","transferType":"in","description":"test","transferAmount":45000,"accumulated":0,"referenceCode":"FT24012345678"}
        """;

        var result = await CreateController(context, body).Receive();

        Assert.IsType<OkObjectResult>(result);
        var order = await context.Orders.SingleAsync(o => o.Id == 1);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.True(await context.Payments.AnyAsync(t => t.Provider == "SePay" && t.ProviderTransactionId == "92704" && t.Status == PaymentStatus.Paid));
    }

    [Fact]
    public async Task Webhook_DuplicatePayload_DoesNotProcessTwice()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        context.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-1",
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentStatus = PaymentStatus.Paid,
            PaymentCode = "FTB7K3P9Q2",
            Total = 45000
        });
        context.Payments.Add(new Payment
        {
            Provider = "SePay",
            ProviderTransactionId = "92704",
            OrderId = 1,
            PaymentCode = "FTB7K3P9Q2",
            Amount = 45000,
            Status = PaymentStatus.Paid,
            ProviderEventStatus = PaymentProviderEventStatus.Accepted,
            Message = "{}"
        });
        await context.SaveChangesAsync();

        const string body = """
        {"id":92704,"code":"FTB7K3P9Q2","transferType":"in","transferAmount":45000,"referenceCode":"FT24012345678"}
        """;

        var result = await CreateController(context, body).Receive();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await context.Payments.CountAsync(t => t.ProviderTransactionId == "92704"));
    }

    [Fact]
    public async Task Webhook_WrongAmount_DoesNotMarkPaid()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        context.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-1",
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentStatus = PaymentStatus.Pending,
            PaymentCode = "FTB7K3P9Q2",
            Total = 45000
        });
        await context.SaveChangesAsync();

        const string body = """
        {"id":92705,"code":"FTB7K3P9Q2","transferType":"in","transferAmount":44000,"referenceCode":"FT24012345678"}
        """;

        var result = await CreateController(context, body).Receive();

        Assert.IsType<OkObjectResult>(result);
        var order = await context.Orders.SingleAsync(o => o.Id == 1);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.True(await context.Payments.AnyAsync(t => t.ProviderTransactionId == "92705" && t.ProviderEventStatus == PaymentProviderEventStatus.Ignored));
    }

    [Fact]
    public async Task Webhook_InvalidHmac_ReturnsUnauthorized()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        const string body = """{"id":92704,"code":"FTB7K3P9Q2"}""";
        var controller = CreateController(context, body);
        controller.Request.Headers["X-SePay-Signature"] = "sha256=bad";

        var result = await controller.Receive();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Webhook_ExpiredTimestamp_ReturnsUnauthorized()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        const string body = """{"id":92704,"code":"FTB7K3P9Q2"}""";
        var controller = CreateController(context, body);
        controller.Request.Headers["X-SePay-Timestamp"] = "0";

        var result = await controller.Receive();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Webhook_MissingSignature_ReturnsUnauthorized()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        const string body = """{"id":92704,"code":"FTB7K3P9Q2"}""";
        var controller = CreateController(context, body);
        controller.Request.Headers.Remove("X-SePay-Signature");

        var result = await controller.Receive();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Webhook_OutgoingTransfer_MarksIgnored()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        context.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-1",
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentStatus = PaymentStatus.Pending,
            PaymentCode = "FTB7K3P9Q2",
            Total = 45000
        });
        await context.SaveChangesAsync();

        const string body = """
        {"id":92706,"code":"FTB7K3P9Q2","transferType":"out","transferAmount":45000,"referenceCode":"FT24012345678"}
        """;

        var result = await CreateController(context, body).Receive();

        Assert.IsType<OkObjectResult>(result);
        var order = await context.Orders.SingleAsync(o => o.Id == 1);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.True(await context.Payments.AnyAsync(t => t.ProviderTransactionId == "92706" && t.ProviderEventStatus == PaymentProviderEventStatus.Ignored));
    }
}
