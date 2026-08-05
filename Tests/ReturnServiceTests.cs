using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Returns;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
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

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipped)]
    public async Task CreateAsync_rejects_orders_not_delivered(OrderStatus status)
    {
        await using var fixture = await SeedReturnFixtureAsync(status, DateTime.UtcNow);
        var result = await fixture.Service.CreateAsync(
            CreateValidCommand(fixture.Order.Id, fixture.Item.Id, 0.5m, true),
            fixture.Customer.Id);

        Assert.False(result.Success);
        Assert.Empty(await fixture.Context.ReturnRequests.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_rejects_expired_claim_and_damage_without_image()
    {
        await using var expired = await SeedReturnFixtureAsync(
            OrderStatus.Delivered, DateTime.UtcNow.AddHours(-25));
        var expiredResult = await expired.Service.CreateAsync(
            CreateValidCommand(expired.Order.Id, expired.Item.Id, 0.5m, true),
            expired.Customer.Id);

        await using var missingImage = await SeedReturnFixtureAsync(
            OrderStatus.Delivered, DateTime.UtcNow);
        var missingImageResult = await missingImage.Service.CreateAsync(
            CreateValidCommand(missingImage.Order.Id, missingImage.Item.Id, 0.5m, false),
            missingImage.Customer.Id);

        Assert.False(expiredResult.Success);
        Assert.False(missingImageResult.Success);
    }

    [Fact]
    public async Task CreateAsync_persists_decimal_item_and_timeline_event()
    {
        await using var fixture = await SeedReturnFixtureAsync(
            OrderStatus.Delivered, DateTime.UtcNow);
        var result = await fixture.Service.CreateAsync(
            CreateValidCommand(fixture.Order.Id, fixture.Item.Id, 0.5m, true),
            fixture.Customer.Id);

        var item = await fixture.Context.ReturnRequestItems.SingleAsync();
        var request = await fixture.Context.ReturnRequests.SingleAsync();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(0.5m, item.RequestedQuantity);
        Assert.Equal(ReturnRequestStatus.Submitted, request.Status);
        Assert.Single(await fixture.Context.ReturnEvents.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_rejects_second_request_for_same_order()
    {
        await using var fixture = await SeedReturnFixtureAsync(
            OrderStatus.Delivered, DateTime.UtcNow);
        var command = CreateValidCommand(fixture.Order.Id, fixture.Item.Id, 0.5m, true);

        var first = await fixture.Service.CreateAsync(command, fixture.Customer.Id);
        Assert.True(first.Success, first.ErrorMessage);
        var second = await fixture.Service.CreateAsync(command, fixture.Customer.Id);

        Assert.False(second.Success);
    }

    [Fact]
    public async Task AddCustomerInfoAsync_rejects_expired_supplement_and_closes_request()
    {
        await using var fixture = await SeedReturnFixtureAsync(
            OrderStatus.Delivered, DateTime.UtcNow);
        var created = await fixture.Service.CreateAsync(
            CreateValidCommand(fixture.Order.Id, fixture.Item.Id, 0.5m, true),
            fixture.Customer.Id);
        var requestInfo = await fixture.Service.RequestCustomerInfoAsync(
            new RequestCustomerInfoCommand(created.ReturnRequestId!.Value, "Vui lòng gửi thêm ảnh.", created.RowVersion!),
            fixture.Admin.Id);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(25);

        var result = await fixture.Service.AddCustomerInfoAsync(
            new SupplementReturnCommand(created.ReturnRequestId.Value, "Đã gửi thêm ảnh.", [], requestInfo.RowVersion!),
            fixture.Customer.Id);
        var request = await fixture.Context.ReturnRequests.SingleAsync();

        Assert.False(result.Success);
        Assert.Equal(ReturnRequestStatus.Rejected, request.Status);
        Assert.Contains(await fixture.Context.ReturnEvents.ToListAsync(),
            item => item.EventType == ReturnEventType.Rejected);
    }

    [Fact]
    public async Task AddCustomerInfoAsync_allows_only_one_submission_within_24_hours()
    {
        await using var fixture = await SeedReturnFixtureAsync(
            OrderStatus.Delivered, DateTime.UtcNow);
        var created = await fixture.Service.CreateAsync(
            CreateValidCommand(fixture.Order.Id, fixture.Item.Id, 0.5m, true),
            fixture.Customer.Id);
        Assert.True(created.Success, created.ErrorMessage);
        var requestId = created.ReturnRequestId!.Value;

        var requestInfo = await fixture.Service.RequestCustomerInfoAsync(
            new RequestCustomerInfoCommand(requestId, "Vui lòng gửi thêm ảnh.", created.RowVersion!),
            fixture.Admin.Id);
        var first = await fixture.Service.AddCustomerInfoAsync(
            new SupplementReturnCommand(requestId, "Đã gửi thêm ảnh.", [], requestInfo.RowVersion!),
            fixture.Customer.Id);
        var second = await fixture.Service.AddCustomerInfoAsync(
            new SupplementReturnCommand(requestId, "Gửi lại.", [], first.RowVersion!),
            fixture.Customer.Id);

        Assert.True(requestInfo.Success, requestInfo.ErrorMessage);
        Assert.True(first.Success, first.ErrorMessage);
        Assert.False(second.Success);
    }

    private static async Task<ReturnFixture> SeedReturnFixtureAsync(
        OrderStatus status,
        DateTime deliveredAtUtc,
        decimal quantity = 2m)
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        var context = new ApplicationDbContext(options);
        var category = new Category { Id = 100, Name = "Fruit", Slug = "return-fruit" };
        var product = new Product
        {
            Id = 100,
            Category = category,
            Name = "Apple",
            Slug = $"return-apple-{Guid.NewGuid():N}",
            Unit = "kg",
            Price = 100_000m,
            StockQuantity = 10m,
            MinOrderQuantity = 0.1m,
            IsActive = true
        };
        var customer = new User
        {
            Id = 10,
            Name = "Customer",
            Email = $"customer-{Guid.NewGuid():N}@example.test",
            Password = "hash"
        };
        var admin = new User
        {
            Id = 20,
            Name = "Admin",
            Email = $"admin-{Guid.NewGuid():N}@example.test",
            Password = "hash",
            Role = UserRole.Admin
        };
        var order = new Order
        {
            Id = 100,
            User = customer,
            OrderNumber = $"ORD-RETURN-{Guid.NewGuid():N}"[..24],
            Status = status,
            DeliveredAtUtc = deliveredAtUtc,
            PaymentStatus = PaymentStatus.Paid,
            ShippingFee = 20_000m,
            Items =
            [
                new OrderItem
                {
                    Id = 100,
                    Product = product,
                    ProductName = product.Name,
                    Quantity = quantity,
                    BasePrice = product.Price,
                    Price = product.Price,
                    Total = quantity * product.Price
                }
            ]
        };
        order.Subtotal = quantity * product.Price;
        order.Total = order.Subtotal + order.ShippingFee;

        context.Categories.Add(category);
        context.Users.AddRange(customer, admin);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var imageUpload = new Mock<IImageUploadService>();
        imageUpload.Setup(service => service.IsValidImageFile(It.IsAny<IFormFile>())).Returns(true);
        imageUpload.Setup(service => service.IsValidFileSize(It.IsAny<IFormFile>(), It.IsAny<long>())).Returns(true);
        imageUpload.Setup(service => service.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync((IFormFile file, string folder) => $"{folder}/{Guid.NewGuid():N}.jpg");

        var now = DateTime.UtcNow;
        var clock = new FixedTimeProvider(now);
        return new ReturnFixture
        {
            Context = context,
            Service = new ReturnService(context, imageUpload.Object, clock),
            Order = order,
            Item = order.Items.Single(),
            Product = product,
            Customer = customer,
            Admin = admin,
            Clock = clock
        };
    }

    private static CreateReturnCommand CreateValidCommand(
        int orderId,
        int orderItemId,
        decimal quantity,
        bool includeEvidence)
    {
        IReadOnlyList<IFormFile> evidence = includeEvidence
            ? [new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "evidence", "evidence.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            }]
            : [];

        return new CreateReturnCommand(
            orderId,
            [new CreateReturnItemCommand(
                orderItemId,
                quantity,
                ReturnReasonCode.Damaged,
                "Hàng bị dập.",
                evidence)]);
    }

    private sealed class ReturnFixture : IAsyncDisposable
    {
        public required ApplicationDbContext Context { get; init; }
        public required ReturnService Service { get; init; }
        public required Order Order { get; init; }
        public required OrderItem Item { get; init; }
        public required Product Product { get; init; }
        public required User Customer { get; init; }
        public required User Admin { get; init; }
        public required FixedTimeProvider Clock { get; init; }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
