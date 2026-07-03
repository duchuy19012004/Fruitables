using System.Net;
using System.Text;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
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
        Assert.Equal("test-token", handler.LastRequest!.Headers.GetValues("Token").Single());
        Assert.Equal("885", handler.LastRequest!.Headers.GetValues("ShopId").Single());
    }

    [Fact]
    public async Task CalculateFeeAsync_ReturnsNull_WhenGhnFails()
    {
        var handler = new StubHttpMessageHandler("""{"code":400,"message":"Bad request"}""", HttpStatusCode.BadRequest);
        var service = CreateService(handler);

        var fee = await service.CalculateFeeAsync(1454, "21211", 1000, 20, 15, 10);

        Assert.Null(fee);
    }

    [Fact]
    public async Task CalculateFeeAsync_UsesConfiguredBaseUrl_WhenClientHasNoBaseAddress()
    {
        var handler = new StubHttpMessageHandler("""{"code":200,"data":{"total":32000}}""", HttpStatusCode.OK);
        var service = CreateService(handler, setBaseAddress: false);

        var fee = await service.CalculateFeeAsync(1454, "21211", 1000, 20, 15, 10);

        Assert.Equal(32000, fee);
        Assert.Equal(
            "https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/fee",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CalculateFeeAsync_ReturnsNull_WhenBaseUrlInvalidAndClientHasNoBaseAddress()
    {
        var handler = new StubHttpMessageHandler("""{"code":200,"data":{"total":32000}}""", HttpStatusCode.OK);
        var service = CreateService(handler, setBaseAddress: false, baseUrl: "not a url");

        var fee = await service.CalculateFeeAsync(1454, "21211", 1000, 20, 15, 10);

        Assert.Null(fee);
    }

    [Fact]
    public async Task ResolveAddressAsync_ReturnsAddressCode_WhenGhnMasterDataMatches()
    {
        var handler = new StubHttpMessageHandler(
            ("""{"code":200,"data":[{"ProvinceID":201,"ProvinceName":"Ha Noi"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"DistrictID":1442,"DistrictName":"Quan Ba Dinh"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"WardCode":"20101","WardName":"Phuong Phuc Xa"}]}""", HttpStatusCode.OK));
        var service = CreateService(handler);

        var addressCode = await service.ResolveAddressAsync("Ha Noi", "Phuc Xa");

        Assert.Equal(new GhnAddressCode(1442, "20101"), addressCode);
        Assert.Contains(handler.Requests, request => request.RequestUri!.ToString().Contains("district_id=1442"));
    }

    [Fact]
    public async Task ResolveAddressAsync_MatchesProvinceWithAdministrativePrefix()
    {
        var handler = new StubHttpMessageHandler(
            ("""{"code":200,"data":[{"ProvinceID":203,"ProvinceName":"Da Nang"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"DistrictID":1527,"DistrictName":"Quan Thanh Khe"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"WardCode":"40201","WardName":"Phuong An Khe"}]}""", HttpStatusCode.OK));
        var service = CreateService(handler);

        var addressCode = await service.ResolveAddressAsync("Thanh pho Da Nang", "Phuong An Khe");

        Assert.Equal(new GhnAddressCode(1527, "40201"), addressCode);
    }

    [Fact]
    public async Task ResolveAddressAsync_ReturnsNull_WhenGhnMasterDataCodeFails()
    {
        var handler = new StubHttpMessageHandler(
            ("""{"code":400,"data":[{"ProvinceID":201,"ProvinceName":"Ha Noi"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"DistrictID":1442,"DistrictName":"Quan Ba Dinh"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"WardCode":"20101","WardName":"Phuong Phuc Xa"}]}""", HttpStatusCode.OK));
        var service = CreateService(handler);

        var addressCode = await service.ResolveAddressAsync("Ha Noi", "Phuc Xa");

        Assert.Null(addressCode);
    }

    [Fact]
    public async Task ResolveAddressAsync_ReturnsNull_WhenWardNameMatchesMultipleDistricts()
    {
        var handler = new StubHttpMessageHandler(
            ("""{"code":200,"data":[{"ProvinceID":201,"ProvinceName":"Ha Noi"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"DistrictID":1442,"DistrictName":"Quan Ba Dinh"},{"DistrictID":1443,"DistrictName":"Quan Hoan Kiem"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"WardCode":"20101","WardName":"Phuong Phuc Xa"}]}""", HttpStatusCode.OK),
            ("""{"code":200,"data":[{"WardCode":"20102","WardName":"Phuong Phuc Xa"}]}""", HttpStatusCode.OK));
        var service = CreateService(handler);

        var addressCode = await service.ResolveAddressAsync("Ha Noi", "Phuc Xa");

        Assert.Null(addressCode);
    }

    private static GhnService CreateService(HttpMessageHandler handler, bool setBaseAddress = true, string? baseUrl = null)
    {
        var httpClient = new HttpClient(handler);
        if (setBaseAddress)
            httpClient.BaseAddress = new Uri("https://dev-online-gateway.ghn.vn/shiip/public-api/");

        var options = Options.Create(new GhnOptions
        {
            BaseUrl = baseUrl ?? "https://dev-online-gateway.ghn.vn/shiip/public-api/",
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
        private readonly Queue<(string Json, HttpStatusCode StatusCode)> _responses;

        public HttpRequestMessage? LastRequest { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHttpMessageHandler(string json, HttpStatusCode statusCode)
            : this((json, statusCode))
        {
        }

        public StubHttpMessageHandler(params (string Json, HttpStatusCode StatusCode)[] responses)
        {
            _responses = new Queue<(string Json, HttpStatusCode StatusCode)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);
            var response = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Json, Encoding.UTF8, "application/json")
            });
        }
    }
}
