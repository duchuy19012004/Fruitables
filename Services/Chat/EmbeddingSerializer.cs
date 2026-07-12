using System.Text.Json;

namespace Fruitables.Services.Chat;

/// <summary>
/// JSON serialization for embedding vectors stored as <c>float[]</c>.
/// </summary>
public static class EmbeddingSerializer
{
    public static string ToJson(float[] vector)
    {
        return JsonSerializer.Serialize(vector ?? Array.Empty<float>());
    }

    public static float[] FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<float>();
        }

        return JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
    }
}
