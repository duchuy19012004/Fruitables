using System.Text;
using Fruitables.Data;
using Fruitables.Models.Json;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public sealed class AggregateJsonPerformanceTests
{
    private static readonly VersionedJsonSerializer Serializer = new();

    [Theory]
    [InlineData("cart", 65536)]
    [InlineData("review", 32768)]
    [InlineData("return", 262144)]
    [InlineData("chat", 524288)]
    public void Normal_aggregate_documents_stay_below_size_gate(string aggregate, int limit)
    {
        var json = aggregate switch
        {
            "cart" => Serializer.Serialize(new CartLinesDocument
            {
                Lines =
                [
                    new CartLineDocument { ProductId = 1, Quantity = 1, Price = 100, ComboDiscount = 0 }
                ]
            }),
            "review" => Serializer.Serialize(new ReviewMetadataDocument
            {
                CreatedAt = DateTime.UtcNow,
                Reports = [new ReviewReportEntry
                {
                    Id = 1,
                    ReportedByUserId = 2,
                    Reason = Fruitables.Models.ReportReason.Spam,
                    Status = Fruitables.Models.ReportStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                }]
            }),
            "return" => Serializer.Serialize(new ReturnDetailsDocument
            {
                SubmittedAtUtc = DateTime.UtcNow,
                ClaimDeadlineAtUtc = DateTime.UtcNow.AddDays(1)
            }),
            _ => Serializer.Serialize(new ChatMessagesDocument
            {
                Messages = [new ChatMessageDocument
                {
                    Role = "assistant",
                    Content = "Câu trả lời ngắn",
                    CreatedAt = DateTime.UtcNow
                }]
            })
        };

        Assert.InRange(Encoding.UTF8.GetByteCount(json), 1, limit);
    }

    [Fact]
    public async Task Product_list_projection_does_not_load_unrelated_aggregate_columns()
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        await using var db = new ApplicationDbContext(options);

        _ = await db.Products.AsNoTracking()
            .Select(product => new { product.Id, product.Name, product.Price })
            .ToListAsync();

        Assert.Equal(1, interceptor.ProductSelectCount);
    }
}
