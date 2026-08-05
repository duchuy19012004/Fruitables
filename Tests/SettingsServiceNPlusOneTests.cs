using Xunit;
using Moq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Infrastructure;

namespace Fruitables.Tests;

// Guardrail N+1: SaveSettingsAsync phải lookup Settings theo batch (1 SELECT cho mọi
// số key), không truy vấn từng key. Test với 1, 5, 20 key.
public class SettingsServiceNPlusOneTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task SaveSettingsAsync_SettingsLookupIsBatched(int keyCount)
    {
        var (interceptor, options) = SeedSettings(keyCount);
        interceptor.Register("Settings");

        var newSettings = Enumerable.Range(1, keyCount)
            .ToDictionary(i => $"new_key_{i}", _ => (string?)$"new_value_{1}");

        using var context = new ApplicationDbContext(options);
        var service = new SettingsService(
            new UnitOfWork(context),
            Mock.Of<IMemoryCache>(),
            Mock.Of<IWebHostEnvironment>(),
            Mock.Of<IIndexingService>(),
            NullLogger<SettingsService>.Instance);

        var result = await service.SaveSettingsAsync(newSettings, group: "general");

        Assert.True(result.Success);
        // 1 SELECT Settings cho batch lookup; không phụ thuộc keyCount.
        Assert.Equal(1, interceptor.GetCount("Settings"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task SaveSettingAsync_SingleKey_OneLookup(int keyCount)
    {
        // Sanity: a single key saves still use a single SELECT Settings for the lookup path.
        var (interceptor, options) = SeedSettings(0);
        interceptor.Register("Settings");

        using var context = new ApplicationDbContext(options);
        var service = new SettingsService(
            new UnitOfWork(context),
            Mock.Of<IMemoryCache>(),
            Mock.Of<IWebHostEnvironment>(),
            Mock.Of<IIndexingService>(),
            NullLogger<SettingsService>.Instance);

        for (int i = 1; i <= keyCount; i++)
        {
            var r = await service.SaveSettingAsync($"single_key_{i}", $"v_{i}");
            Assert.True(r.Success);
        }

        // SaveSettingAsync issue 1 SELECT mỗi lần gọi. Tổng = keyCount, không phải N+1 theo item.
        Assert.Equal(keyCount, interceptor.GetCount("Settings"));
    }

    private static (CountingQueryInterceptor interceptor, DbContextOptions<ApplicationDbContext> options)
        SeedSettings(int existingCount)
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        using var context = new ApplicationDbContext(options);

        for (int i = 1; i <= existingCount; i++)
        {
            context.Settings.Add(new Setting
            {
                Key = $"existing_key_{i}",
                Value = $"existing_value_{i}",
                Group = "general"
            });
        }
        context.SaveChanges();

        return (interceptor, options);
    }
}
