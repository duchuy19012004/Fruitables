using System.Net.Http.Json;
using System.Text.Json;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat;

// ============================================================
// GỌI API /embeddings (khi cấu hình EmbeddingProvider = OpenAICompatible)
//
// Hiện mặc định app dùng LocalHashEmbeddingClient (không cần file này).
// Giữ lại để sau này nếu có nhà cung cấp embed tương thích OpenAI thì bật lại.
// ============================================================
public sealed class SpaceXaiEmbeddingClient : IEmbeddingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ChatOptions _options;
    private readonly ILogger<SpaceXaiEmbeddingClient> _logger;

    public SpaceXaiEmbeddingClient(
        HttpClient httpClient,
        IOptions<ChatOptions> options,
        ILogger<SpaceXaiEmbeddingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var body = new
        {
            model = _options.EmbeddingModel,
            input = text
        };

        using var response = await _httpClient.PostAsJsonAsync("embeddings", body, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Embedding request failed with status {StatusCode}: {Body}",
                (int)response.StatusCode,
                errorBody);
            throw new InvalidOperationException("LLM provider error");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return SpaceXaiResponseParser.ParseEmbedding(json);
    }
}
