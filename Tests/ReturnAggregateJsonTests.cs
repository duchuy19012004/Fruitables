using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Models.Returns;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Returns;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public sealed class ReturnAggregateJsonTests
{
    private static readonly VersionedJsonSerializer Serializer = new();

    [Fact]
    public async Task Create_return_mirrors_return_case_details_json()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);

        db.Users.Add(new User
        {
            Id = 7,
            Name = "Customer",
            Email = "c@example.com",
            Password = "x",
            Role = UserRole.Customer,
            IsActive = true
        });
        db.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
        db.Products.Add(new Product
        {
            Id = 1,
            CategoryId = 1,
            Name = "Tao",
            Slug = "tao",
            Price = 100,
            Unit = "kg",
            IsActive = true
        });
        db.Orders.Add(new Order
        {
            Id = 11,
            UserId = 7,
            OrderNumber = "ORD-R1",
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Paid,
            Total = 100,
            Subtotal = 100,
            DeliveredAtUtc = DateTime.UtcNow.AddHours(-1),
            Items =
            [
                new OrderItem
                {
                    Id = 21,
                    ProductId = 1,
                    ProductName = "Tao",
                    Quantity = 1,
                    Price = 100,
                    BasePrice = 100,
                    Total = 100
                }
            ]
        });
        await db.SaveChangesAsync();

        var upload = new Mock<IImageUploadService>();
        upload.Setup(service => service.IsValidImageFile(It.IsAny<IFormFile>())).Returns(true);
        upload.Setup(service => service.IsValidFileSize(It.IsAny<IFormFile>(), It.IsAny<long>())).Returns(true);
        upload.Setup(service => service.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("returns/a.jpg");

        var service = new ReturnService(db, upload.Object, TimeProvider.System, Serializer);
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "a.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await service.CreateAsync(new CreateReturnCommand(
            OrderId: 11,
            Items:
            [
                new CreateReturnItemCommand(
                    OrderItemId: 21,
                    RequestedQuantity: 1,
                    Reason: ReturnReasonCode.Damaged,
                    Description: "vo",
                    Evidence: [file])
            ]), 7);

        Assert.True(result.Success, result.ErrorMessage);
        var target = await db.Returns.SingleAsync();
        Assert.Equal(11, target.OrderId);
        var details = Serializer.Deserialize<ReturnDetailsDocument>(target.DetailsJson);
        Assert.Single(details.Items);
        Assert.Equal(21, details.Items[0].OrderItemId);
        Assert.NotEmpty(details.Evidence);
    }
}
