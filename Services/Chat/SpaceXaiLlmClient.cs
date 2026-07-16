using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat;

// ============================================================
// GỌI AI CHAT THEO CHUẨN OPENAI
//
// Dùng được với nhiều nhà: Kimi (Moonshot), xAI, OpenAI...
// Chỉ cần đổi BaseUrl + ApiKey + Model trong cấu hình.
//
// Tên class còn "SpaceXai" vì lịch sử ban đầu; thực tế là client chung.
// ============================================================
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
        // kimi-k2.7-code (và một số model thinking) chỉ chấp nhận temperature = 1
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

            var delta = SpaceXaiResponseParser.TryParseStreamDeltaContent(payload);
            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }

    // k2.7-code: temperature cố định 1; model khác dùng 0.2 (ổn định hơn cho CS)
    internal static double ResolveTemperature(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return 0.2;

        var m = model.Trim().ToLowerInvariant();
        if (m.Contains("k2.7") || m.Contains("kimi-k2.7"))
            return 1.0;

        return 0.2;
    }
}
