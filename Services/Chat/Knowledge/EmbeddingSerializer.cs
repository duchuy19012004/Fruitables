using System.Text.Json;

namespace Fruitables.Services.Chat.Knowledge;

// ============================================================
// Đổi vector số <-> chuỗi JSON để lưu vào database.
// Ví dụ: [0.1, 0.2]  <->  "[0.1,0.2]"
// ============================================================
public static class EmbeddingSerializer
{
    // Vector → chữ JSON lưu DB
    public static string ToJson(float[] vector) =>
        JsonSerializer.Serialize(vector ?? Array.Empty<float>());

    // Chữ JSON trong DB → vector để so sánh
    public static float[] FromJson(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<float>()
            : (JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>());
}
