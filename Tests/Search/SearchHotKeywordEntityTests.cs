// Tests/Search/SearchHotKeywordEntityTests.cs
using Fruitables.Data;
using Fruitables.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests.Search;

public class SearchHotKeywordEntityTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Can_persist_hot_keyword()
    {
        await using var db = CreateContext();
        db.SearchHotKeywords.Add(new SearchHotKeyword
        {
            Text = "Táo Fuji",
            NormalizedText = "tao fuji",
            Weight = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var row = await db.SearchHotKeywords.SingleAsync(k => k.Weight == 10 && k.Text == "Táo Fuji");
        Assert.Equal("Táo Fuji", row.Text);
        Assert.Equal("tao fuji", row.NormalizedText);
        Assert.True(row.IsActive);
    }
}
