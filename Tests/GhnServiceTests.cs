using System.Net;
using System.Text;
using Fruitables.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fruitables.Tests;

public class GhnServiceTests
{
    [Fact]
    public async Task CalculateFeeAsync_ReturnsTotal_WhenGhnReturnsSuccess()
    {
        var handler = new StubHttpMessageHandler("""
        {
          "code": 200,
          "message": "Success",
          "data": { "total": 32000 }
        }
        """, HttpStatusCode.OK);

        var service = CreateService(handler);

        var fee = await service.CalculateFeeAsync(1454, "21211", 1000, 20, 15, 10);

        Assert.Equal(32000, fee);
        Assert.Equal("Token", handler.LastRequest!.Headers.First(h => h.Key == "Token").Key);
        Assert.Equal("ShopId", handler.LastRequest!.Headers.First(h => h.Key == "ShopId").Key);
    }

    [Fact]
    public async Task CalculateFeeAsync_ReturnsNull_WhenGhnFails()
    {
        var handler = new StubHttpMessageHandler("""{"code":400,"message":"Bad request"}""", HttpStatusCode.BadRequest);
        var service = CreateService(handler);

        var fee = await service.CalculateFeeAsync(1454, "21211", 1000, 20, 15, 10);

        Assert.Null(fee);
    }

    private static GhnService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev-online-gateway.ghn.vn/shiip/public-api/")
        };

        var options = Options.Create(new GhnOptions
        {
            BaseUrl = "https://dev-online-gateway.ghn.vn/shiip/public-api/",
            Token = "test-token",
            ShopId = 885,
            FromDistrictId = 1447,
            FromWardCode = "20101",
            ServiceTypeId = 2,
            DefaultWeight = 1000,
            DefaultLength = 20,
            DefaultWidth = 15,
            DefaultHeight = 10
        });

        return new GhnService(httpClient, options, NullLogger<GhnService>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _json;
        private readonly HttpStatusCode _statusCode;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHttpMessageHandler(string json, HttpStatusCode statusCode)
        {
            _json = json;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }
}
