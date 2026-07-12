using System.Security.Cryptography;
using System.Text;
using Fruitables.Services.Interfaces;

namespace Fruitables.Tests.Chat.Fakes;

/// <summary>
/// Test double that produces stable, unit-length embedding vectors from text.
/// Uses SHA256 for a base vector plus a bag-of-tokens boost so similar strings
/// share overlapping dimensions (useful for cosine-similarity smoke tests).
/// </summary>
public sealed class DeterministicEmbeddingClient : IEmbeddingClient
{
    public int Dimensions { get; }

    public DeterministicEmbeddingClient(int dimensions = 32)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be positive.");
        }

        Dimensions = dimensions;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var vector = new float[Dimensions];
        var input = text ?? string.Empty;

        // Base vector from full-text hash so identical strings map to identical vectors.
        var fullHash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        for (var i = 0; i < Dimensions; i++)
        {
            // Map bytes into [-1, 1]
            vector[i] = (fullHash[i % fullHash.Length] / 127.5f) - 1f;
        }

        // Token bag boost: shared tokens push the same dimensions, so similar
        // phrases get higher cosine similarity than unrelated text.
        foreach (var token in Tokenize(input))
        {
            var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = tokenHash[0] % Dimensions;
            var magnitude = 0.35f + (tokenHash[1] / 255f) * 0.65f;
            var sign = (tokenHash[2] & 1) == 0 ? 1f : -1f;
            vector[index] += sign * magnitude;

            // Secondary dim for a little more separation without wiping bag overlap.
            var index2 = tokenHash[3] % Dimensions;
            if (index2 != index)
            {
                vector[index2] += sign * magnitude * 0.5f;
            }
        }

        L2Normalize(vector);
        return Task.FromResult(vector);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

    private static void L2Normalize(float[] vector)
    {
        double sumSq = 0;
        for (var i = 0; i < vector.Length; i++)
        {
            sumSq += vector[i] * vector[i];
        }

        if (sumSq <= 0)
        {
            // Degenerate empty input: put unit mass on dim 0 for a well-defined vector.
            vector[0] = 1f;
            return;
        }

        var inv = 1.0 / Math.Sqrt(sumSq);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] * inv);
        }
    }
}
