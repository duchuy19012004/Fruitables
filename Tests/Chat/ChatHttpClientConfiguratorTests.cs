using System.Net.Http.Headers;
using Fruitables.Options;
using Fruitables.Services.Chat.Providers;
using Xunit;

namespace Fruitables.Tests.Chat;

public class ChatHttpClientConfiguratorTests
{
    [Fact]
    public void Configure_usesLocalEndpointWithoutAuthorizationHeader()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "legacy-kimi-key");
        var options = new ChatOptions
        {
            BaseUrl = "http://localhost:20128/v1",
            Model = "cx/gpt-5.6-luna"
        };

        ChatHttpClientConfigurator.Configure(client, options);

        Assert.Equal(new Uri("http://localhost:20128/v1/"), client.BaseAddress);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.Equal(TimeSpan.FromSeconds(120), client.Timeout);
    }
}
