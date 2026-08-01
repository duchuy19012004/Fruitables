using System.Text.Json;

namespace Fruitables.Services.Chat;

// ============================================================
// ĐỌC JSON TRẢ VỀ TỪ API AI (chuẩn OpenAI)
//
// AI trả về một "gói JSON" dài; ta chỉ lấy phần chữ trả lời
// hoặc dãy số embedding bên trong.
// ============================================================
public static class OpenAiResponseParser
{
    // Lấy chữ bot trả lời: choices[0].message.content
    public static string ParseChatCompletionContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Unexpected chat completion payload.");

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Phải có mảng choices và ít nhất 1 phần tử
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
                throw new InvalidOperationException("Unexpected chat completion payload.");

            return text;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Unexpected chat completion payload.", ex);
        }
    }

    // Lấy mảnh chữ từ 1 dòng SSE OpenAI: choices[0].delta.content
    // Trả null nếu dòng không có content (role-only, empty, [DONE], v.v.)
    public static string? TryParseStreamDeltaContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var trimmed = json.Trim();
        if (trimmed is "[DONE]" or "\"[DONE]\"")
            return null;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var first = choices[0];
            if (!first.TryGetProperty("delta", out var delta)
                || delta.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!delta.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return content.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Lấy vector: data[0].embedding = [số, số, ...]
    public static float[] ParseEmbedding(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Unexpected embedding payload.");

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
                    throw new InvalidOperationException("Unexpected embedding payload.");

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
