using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Fruitables.Options;
using Fruitables.Services.Communications;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat.Providers;

// ============================================================
// GỌI AI CHAT THEO CHUẨN OPENAI
//
// Gọi endpoint theo chuẩn OpenAI-compatible.
// Chỉ cần đổi BaseUrl + Model trong cấu hình.
// ============================================================
public sealed class OpenAiLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ChatOptions _options;
    private readonly ILogger<OpenAiLlmClient> _logger;

    public OpenAiLlmClient(
        HttpClient httpClient,
        IOptions<ChatOptions> options,
        ILogger<OpenAiLlmClient> logger)
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
        var sb = new StringBuilder();
        await foreach (var chunk in CompleteStreamingAsync(systemPrompt, userPrompt, ct))
            sb.Append(chunk);
        return sb.ToString();
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var temperature = ResolveTemperature(_options.Model);
        var body = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature,
            stream = true
        };

        _logger.LogDebug(
            "LLM stream request model={Model} base={BaseAddress}",
            _options.Model,
            _httpClient.BaseAddress);

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "LLM chat stream failed model={Model} status={StatusCode}: {Body}",
                _options.Model,
                (int)response.StatusCode,
                errorBody);
            throw new InvalidOperationException(
                $"LLM provider error (model={_options.Model}, status={(int)response.StatusCode})");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                break;

            if (line.Length == 0)
                continue;

            // SSE: "data: {...}" hoặc "data:{...}"
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = line.AsSpan(5).Trim().ToString();
            if (payload.Length == 0 || payload == "[DONE]")
            {
                if (payload == "[DONE]")
                    yield break;
                continue;
            }

            var delta = OpenAiResponseParser.TryParseStreamDeltaContent(payload);
            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }

    // Gọi LLM và parse JSON response (dùng cho structured output).
    // Bật JSON mode (response_format=json_object) + max_tokens để tránh chuỗi JSON bị cắt giữa chừng.
    public async Task<JsonElement> GenerateAsync(
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
            temperature = 0.1,
            stream = false,
            response_format = new { type = "json_object" },
            max_tokens = _options.MaxTokens
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "LLM generate failed model={Model} status={StatusCode} base={BaseAddress}: {Body}",
                _options.Model,
                (int)response.StatusCode,
                _httpClient.BaseAddress?.ToString(),
                errorBody);
            throw new InvalidOperationException(
                $"LLM provider error (model={_options.Model}, status={(int)response.StatusCode})");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        // OpenAI-compatible: choices[0].message.content
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        // Parse content as JSON
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    internal static double ResolveTemperature(string? model)
    {
        return 0.2;
    }
}
