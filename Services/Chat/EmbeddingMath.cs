namespace Fruitables.Services.Chat;

/// <summary>
/// Vector math helpers for embedding similarity.
/// </summary>
public static class EmbeddingMath
{
    /// <summary>
    /// Cosine similarity of two vectors. Returns 0 for empty, zero-magnitude, or length-mismatched inputs.
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a is null || b is null || a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0f;
        }

        double dot = 0;
        double magA = 0;
        double magB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var x = a[i];
            var y = b[i];
            dot += x * y;
            magA += x * x;
            magB += y * y;
        }

        if (magA == 0 || magB == 0)
        {
            return 0f;
        }

        return (float)(dot / (Math.Sqrt(magA) * Math.Sqrt(magB)));
    }
}
