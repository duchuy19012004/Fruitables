// Tests/Search/SearchSuggestControllerTests.cs
using Fruitables.Controllers.Api;
using Fruitables.Options;
using Fruitables.Services.Communications;
using Fruitables.Services.Search;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Fruitables.Tests.Search;

public class SearchSuggestControllerTests
{
    private static SearchSuggestController Create(
        Mock<ISearchSuggestService> svc,
        SearchSuggestOptions? opt = null,
        IMemoryCache? cache = null)
    {
        var controller = new SearchSuggestController(
            svc.Object,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Microsoft.Extensions.Options.Options.Create(opt ?? new SearchSuggestOptions { RateLimitPerMinute = 60 }),
            NullLogger<SearchSuggestController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10") }
            }
        };
        return controller;
    }

    [Fact]
    public async Task Suggest_returns_ok_payload()
    {
        var svc = new Mock<ISearchSuggestService>();
        svc.Setup(s => s.SuggestAsync("tao", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSuggestResponse
            {
                Query = "tao",
                ViewAllUrl = "/Shop?search=tao",
                Products = new List<SearchSuggestProductDto>
                {
                    new() { Id = 1, Name = "Táo", Slug = "tao", Url = "/Shop/Detail/tao" }
                }
            });

        var result = await Create(svc).Suggest("tao", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SearchSuggestResponse>(ok.Value);
        Assert.Single(body.Products);
    }

    [Fact]
    public async Task Suggest_rate_limit_returns_429()
    {
        var svc = new Mock<ISearchSuggestService>();
        svc.Setup(s => s.SuggestAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSuggestResponse());

        var opt = new SearchSuggestOptions { RateLimitPerMinute = 2 };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = Create(svc, opt, cache);

        Assert.IsType<OkObjectResult>(await controller.Suggest("ab", CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.Suggest("ab", CancellationToken.None));
        var limited = await controller.Suggest("ab", CancellationToken.None);
        var status = Assert.IsType<ObjectResult>(limited);
        Assert.Equal(429, status.StatusCode);
        svc.Verify(s => s.SuggestAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
