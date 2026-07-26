using System.Net.Http.Headers;
using Fruitables.Options;

namespace Fruitables.Services.Chat;

public static class ChatHttpClientConfigurator
{
    public static void Configure(HttpClient client, ChatOptions options)
    {
        var baseUrl = options.BaseUrl?.TrimEnd('/') + "/";
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            client.BaseAddress = baseUri;

        // Thêm API key header nếu có
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
        else
        {
            client.DefaultRequestHeaders.Authorization = null;
        }

        // Local LLM calls can take longer while the model is warming up.
        client.Timeout = TimeSpan.FromSeconds(120);
    }
}
