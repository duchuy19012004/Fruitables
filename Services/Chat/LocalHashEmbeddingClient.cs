using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Fruitables.Services.Chat;

// ============================================================
// M├â H├ôA CHß╗« ΓåÆ D├âY Sß╗É NGAY TR├èN SERVER (kh├┤ng gß╗ìi API ngo├ái)
//
// D├╣ng khi nh├á AI (Kimi) kh├┤ng c├│ dß╗ïch vß╗Ñ "embeddings".
// C├ích hiß╗âu ─æ╞ín giß║ún:
// - C├╣ng mß╗Öt ─æoß║ín chß╗» ΓåÆ lu├┤n ra c├╣ng mß╗Öt d├úy sß╗æ
// - Chß╗» c├│ nhiß╗üu tß╗½ giß╗æng nhau (kß╗â cß║ú synonym) ΓåÆ d├úy sß╗æ "gß║ºn" nhau h╞ín
//
// Sau khi ─æß╗òi c├ích m├ú h├│a (RetrievalText.AlgorithmId), Admin cß║ºn
// bß║Ñm "─Éß╗ông bß╗Ö knowledge" ─æß╗â tß║ío lß║íi d├úy sß╗æ.
// ============================================================
public sealed class LocalHashEmbeddingClient : IEmbeddingClient
{
    private readonly int _dimensions;

    public LocalHashEmbeddingClient(IOptions<ChatOptions> options)
    {
        var dims = options.Value.EmbeddingDimensions;
        _dimensions = dims > 0 ? dims : 256;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var vector = new float[_dimensions];
        var input = text ?? string.Empty;

        // Lß╗¢p 1: hash cß║ú ─æoß║ín ΓåÆ vector nß╗ün ß╗òn ─æß╗ïnh
        // Keep this as light noise; token/synonym features should dominate relevance.
        var fullHash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        for (var i = 0; i < _dimensions; i++)
            vector[i] = ((fullHash[i % fullHash.Length] / 127.5f) - 1f) * 0.03f;

        // Lß╗¢p 2: token + synonym + bigram "─æß║⌐y" c├íc ├┤ trong vector
        foreach (var token in RetrievalText.ExpandTokens(input))
        {
            var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = tokenHash[0] % _dimensions;
            var magnitude = 0.35f + (tokenHash[1] / 255f) * 0.65f;
            var sign = (tokenHash[2] & 1) == 0 ? 1f : -1f;
            vector[index] += sign * magnitude;

            var index2 = tokenHash[3] % _dimensions;
            if (index2 != index)
                vector[index2] += sign * magnitude * 0.5f;
        }

        L2Normalize(vector);
        return Task.FromResult(vector);
    }

    private static void L2Normalize(float[] vector)
    {
        double sumSq = 0;
        for (var i = 0; i < vector.Length; i++)
            sumSq += vector[i] * vector[i];

        if (sumSq <= 0)
        {
            vector[0] = 1f;
            return;
        }

        var inv = 1.0 / Math.Sqrt(sumSq);
        for (var i = 0; i < vector.Length; i++)
            vector[i] = (float)(vector[i] * inv);
    }
}
