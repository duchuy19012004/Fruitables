using System.Net.Http.Json;
using System.Text.Json;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat;

/// <summary>
/// SpaceXAI / xAI OpenAI-compatible chat completions client.
/// </summary>
public sealed class SpaceXaiLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ChatOptions _options;
    private readonly ILogger<SpaceXaiLlmClient> _logger;

    public SpaceXaiLlmClient(
        HttpClient httpClient,
        IOptions<ChatOptions> options,
        ILogger<SpaceXaiLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var body = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", body, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "SpaceXAI chat completion failed with status {StatusCode}: {Body}",
                (int)response.StatusCode,
                errorBody);
            throw new InvalidOperationException("LLM provider error");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return SpaceXaiResponseParser.ParseChatCompletionContent(json);
    }
}
