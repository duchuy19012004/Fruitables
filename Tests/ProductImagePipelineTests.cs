using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Chat.Knowledge;

namespace Fruitables.Tests;

public sealed class ProductImagePipelineTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(
        Path.GetTempPath(),
        "Fruitables.Tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(1200, 600)]
    [InlineData(600, 1200)]
    [InlineData(800, 800)]
    public async Task UploadProductImageAsync_PreservesAspectRatioWithoutCanvas(int width, int height)
    {
        Directory.CreateDirectory(_webRoot);
        var service = CreateImageService();
        var file = await CreateImageFileAsync(width, height, Color.Red, "product.png", "image/png");

        var url = await service.UploadProductImageAsync(file);

        Assert.EndsWith(".webp", url, StringComparison.OrdinalIgnoreCase);
        var outputPath = Path.Combine(_webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(outputPath));

        using var output = await Image.LoadAsync<Rgba32>(outputPath);
        var expectedWidth = width >= height ? 1000 : (int)Math.Round(1000d * width / height);
        var expectedHeight = height >= width ? 1000 : (int)Math.Round(1000d * height / width);
        Assert.Equal(expectedWidth, output.Width);
        Assert.Equal(expectedHeight, output.Height);
        Assert.True(output[0, 0].R > 180 && output[0, 0].G < 80 && output[0, 0].B < 80);
        var center = output[output.Width / 2, output.Height / 2];
        Assert.True(center.R > 180 && center.G < 80 && center.B < 80);
        Assert.True(output[output.Width - 1, output.Height - 1].R > 180);
    }

    [Fact]
    public async Task UploadProductImageAsync_RejectsCorruptImageContent()
    {
        Directory.CreateDirectory(_webRoot);
        var service = CreateImageService();
        var stream = new MemoryStream("not an image"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "image", "fake.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UploadProductImageAsync(file));

        Assert.Contains("không phải ảnh", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadProductImageAsync_WhitensNearWhiteBackgroundConnectedToEdges()
    {
        Directory.CreateDirectory(_webRoot);
        var service = CreateImageService();
        var stream = new MemoryStream();
        using (var source = new Image<Rgba32>(600, 400, new Rgba32(242, 244, 243)))
        {
            for (var y = 120; y < 280; y++)
            for (var x = 200; x < 400; x++)
                source[x, y] = Color.Red;
            await source.SaveAsync(stream, new PngEncoder());
        }
        stream.Position = 0;
        var file = new FormFile(stream, 0, stream.Length, "image", "gray-background.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var url = await service.UploadProductImageAsync(file);
        var outputPath = Path.Combine(_webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        using var output = await Image.LoadAsync<Rgba32>(outputPath);

        Assert.True(output[10, 10].R > 248 && output[10, 10].G > 248 && output[10, 10].B > 248);
        var center = output[output.Width / 2, output.Height / 2];
        Assert.True(center.R > 180 && center.G < 80 && center.B < 80);
    }

    [Fact]
    public async Task AddImagesAsync_RemovesUploadedFilesWhenBatchFails()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 50_000 });
        await context.SaveChangesAsync();

        var imageService = new Mock<IImageUploadService>();
        imageService.Setup(service => service.IsValidImageFile(It.IsAny<IFormFile>())).Returns(true);
        imageService.Setup(service => service.IsValidFileSize(It.IsAny<IFormFile>(), It.IsAny<long>())).Returns(true);
        imageService
            .SetupSequence(service => service.UploadProductImageAsync(
                It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/products/first.webp")
            .ThrowsAsync(new InvalidOperationException("Ảnh thứ hai không hợp lệ"));

        var service = CreateProductAdminService(context, imageService.Object);
        var files = new List<IFormFile> { Mock.Of<IFormFile>(), Mock.Of<IFormFile>() };

        var result = await service.AddImagesAsync(1, files);

        Assert.False(result.Success);
        Assert.Empty(context.ProductImages);
        imageService.Verify(
            candidate => candidate.DeleteImageAsync("/uploads/products/first.webp"),
            Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_PromotesNextImageWhenPrimaryIsDeleted()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 50_000 });
        context.ProductImages.AddRange(
            new ProductImage { Id = 10, ProductId = 1, ImageUrl = "/uploads/products/one.webp", IsPrimary = true, SortOrder = 0 },
            new ProductImage { Id = 11, ProductId = 1, ImageUrl = "/uploads/products/two.webp", SortOrder = 1 });
        await context.SaveChangesAsync();

        var imageService = new Mock<IImageUploadService>();
        var service = CreateProductAdminService(context, imageService.Object);

        var result = await service.DeleteImageAsync(1, 10);

        Assert.True(result.Success);
        Assert.Null(await context.ProductImages.FindAsync(10));
        Assert.True((await context.ProductImages.FindAsync(11))!.IsPrimary);
        imageService.Verify(
            candidate => candidate.DeleteImageAsync("/uploads/products/one.webp"),
            Times.Once);
    }

    private ImageUploadService CreateImageService()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(candidate => candidate.WebRootPath).Returns(_webRoot);
        return new ImageUploadService(environment.Object);
    }

    private static async Task<IFormFile> CreateImageFileAsync(
        int width,
        int height,
        Color color,
        string fileName,
        string contentType)
    {
        var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(width, height, color))
            await image.SaveAsync(stream, new PngEncoder());
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "image", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProductAdminService CreateProductAdminService(
        ApplicationDbContext context,
        IImageUploadService imageUploadService) => new(
            context,
            imageUploadService,
            Mock.Of<IIndexingService>(),
            NullLogger<ProductAdminService>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }
}
