namespace Fruitables.Services.Chat.Knowledge;

// ============================================================
// Toán đơn giản để so 2 "vân tay số" (vector embedding) giống nhau bao nhiêu.
//
// Cosine similarity:
// - Gần 1  = rất giống (cùng chủ đề)
// - Gần 0  = không liên quan
// - Âm     = hướng ngược (hiếm khi gặp sau khi đã chuẩn hóa)
// ============================================================
public static class EmbeddingMath
{
    public static float CosineSimilarity(float[] a, float[] b)
    {
        // Không so được nếu rỗng hoặc khác độ dài
        if (a is null || b is null || a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0f;

        double dot = 0; // tích vô hướng
        double na = 0;  // độ dài vector a bình phương
        double nb = 0;  // độ dài vector b bình phương

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        // Tránh chia cho 0
        if (na == 0 || nb == 0)
            return 0f;

        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }
}
