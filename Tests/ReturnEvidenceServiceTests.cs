using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Returns;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class ReturnEvidenceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fruitables-return-{Guid.NewGuid():N}");

    [Fact]
    public async Task Upload_ValidPng_UsesRandomKeyAndChecksumOutsideWwwroot()
    {
        await using var db = Context();
        var request = Seed(db);
        var service = Service(db);
        var result = await service.UploadAsync(request.Id, null, request.UserId, UploadFile("../../proof.png", "image/png", Png()), false);
        Assert.True(result.Success);
        Assert.DoesNotContain("proof", result.Evidence!.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, result.Evidence.Sha256Checksum.Length);
        Assert.Equal(EvidenceScanStatus.Pending, result.Evidence.ScanStatus);
        Assert.True(File.Exists(Path.Combine(_root, "App_Data", "ReturnEvidence", result.Evidence.StorageKey)));
    }

    [Fact]
    public async Task Upload_RejectsSpoofedSignatureAndDoesNotWriteFile()
    {
        await using var db = Context();
        var request = Seed(db);
        var result = await Service(db).UploadAsync(request.Id, null, request.UserId, UploadFile("fake.png", "image/png", "not png"u8.ToArray()), false);
        Assert.False(result.Success);
        Assert.Empty(db.ReturnEvidences);
        Assert.False(Directory.Exists(Path.Combine(_root, "App_Data", "ReturnEvidence")));
    }

    [Fact]
    public async Task Upload_RejectsOtherCustomerAndSixthFile()
    {
        await using var db = Context();
        var request = Seed(db);
        var service = Service(db);
        Assert.False((await service.UploadAsync(request.Id, null, request.UserId + 99, UploadFile("proof.png", "image/png", Png()), false)).Success);
        for (var i = 0; i < 5; i++)
            db.ReturnEvidences.Add(new ReturnEvidence { ReturnRequestId = request.Id, UploadedByUserId = request.UserId, OriginalFileName = $"{i}.png", StorageKey = $"{Guid.NewGuid():N}.png", MimeType = "image/png", SizeBytes = 8, Sha256Checksum = new string('a', 64), UploadedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.False((await service.UploadAsync(request.Id, null, request.UserId, UploadFile("six.png", "image/png", Png()), false)).Success);
    }

    [Fact]
    public async Task OpenRead_HidesInternalEvidenceFromCustomerButAllowsAdmin()
    {
        await using var db = Context();
        var request = Seed(db);
        var service = Service(db);
        var uploaded = await service.UploadAsync(request.Id, null, 11, UploadFile("transfer.png", "image/png", Png()), true);
        Assert.True(uploaded.Success);
        Assert.Null(await service.OpenReadAsync(uploaded.Evidence!.Id, request.UserId, false));
        var adminRead = await service.OpenReadAsync(uploaded.Evidence.Id, 11, true);
        Assert.NotNull(adminRead);
        await adminRead!.Value.Content.DisposeAsync();
    }

    private ApplicationDbContext Context() => new(TestDbContextFactory.CreateSqliteOptions());
    private ReturnEvidenceService Service(ApplicationDbContext db)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(_root);
        return new ReturnEvidenceService(db, environment.Object, TimeProvider.System);
    }

    private static ReturnRequest Seed(ApplicationDbContext db)
    {
        var customer = new User { Id = 10, Name = "Customer", Email = "customer@evidence.test", Password = "hash" };
        var admin = new User { Id = 11, Name = "Admin", Email = "admin@evidence.test", Password = "hash", Role = UserRole.Admin };
        var category = new Category { Id = 10, Name = "Fruit", Slug = "evidence-fruit" };
        var product = new Product { Id = 10, Category = category, Name = "Apple", Slug = "evidence-apple", Price = 10 };
        var order = new Order { Id = 10, User = customer, OrderNumber = "EVIDENCE-ORDER", Status = OrderStatus.Delivered, PaymentStatus = PaymentStatus.Paid, DeliveredAtUtc = DateTime.UtcNow, Subtotal = 10, Total = 10 };
        var item = new OrderItem { Id = 10, Order = order, Product = product, ProductName = "Apple", Quantity = 1, BasePrice = 10, Price = 10, Total = 10 };
        var request = new ReturnRequest { Id = 10, ReturnNumber = "RT-EVIDENCE", IdempotencyKey = "evidence-key", Order = order, User = customer, Status = ReturnRequestStatus.Submitted, SubmittedAtUtc = DateTime.UtcNow, ClaimDeadlineAtUtc = DateTime.UtcNow.AddHours(24), ReviewDueAtUtc = DateTime.UtcNow.AddHours(24) };
        db.AddRange(admin, item, request); db.SaveChanges(); return request;
    }

    private static IFormFile UploadFile(string name, string mime, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = mime };
    private static byte[] Png() => [137, 80, 78, 71, 13, 10, 26, 10];
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
