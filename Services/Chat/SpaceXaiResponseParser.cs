using System.Text.Json;

namespace Fruitables.Services.Chat;

/// <summary>
/// Parses OpenAI-compatible JSON payloads from the SpaceXAI / xAI API.
/// </summary>
public static class SpaceXaiResponseParser
{
    /// <summary>
    /// Extracts <c>choices[0].message.content</c> from a chat completion response.
    /// </summary>
    public static string ParseChatCompletionContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Unexpected chat completion payload.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Unexpected chat completion payload.");
            }

            var first = choices[0];
            if (!first.TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("Unexpected chat completion payload.");
            }

            var text = content.GetString();
            if (text is null)
            {
                throw new InvalidOperationException("Unexpected chat completion payload.");
            }

            return text;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Unexpected chat completion payload.", ex);
        }
    }

    /// <summary>
    /// Extracts <c>data[0].embedding</c> as a <see cref="float"/> array from an embeddings response.
    /// </summary>
    public static float[] ParseEmbedding(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Unexpected embedding payload.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Unexpected embedding payload.");
            }

            var first = data[0];
            if (!first.TryGetProperty("embedding", out var embedding)
                || embedding.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Unexpected embedding payload.");
            }

            var length = embedding.GetArrayLength();
            var result = new float[length];
            var index = 0;
            foreach (var item in embedding.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out var value))
                {
                    throw new InvalidOperationException("Unexpected embedding payload.");
                }

                result[index++] = value;
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Unexpected embedding payload.", ex);
        }
    }
}
