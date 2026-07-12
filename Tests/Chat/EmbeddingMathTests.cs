using Fruitables.Services.Chat;
using Xunit;

namespace Fruitables.Tests.Chat;

public class EmbeddingMathTests
{
    [Fact]
    public void CosineSimilarity_identical_unit_vectors_is_one()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 1f, 0f, 0f };

        var sim = EmbeddingMath.CosineSimilarity(a, b);

        Assert.Equal(1f, sim, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_orthogonal_vectors_is_zero()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { 0f, 1f };

        var sim = EmbeddingMath.CosineSimilarity(a, b);

        Assert.Equal(0f, sim, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_length_mismatch_or_empty_returns_zero()
    {
        Assert.Equal(0f, EmbeddingMath.CosineSimilarity(new float[] { 1f }, new float[] { 1f, 2f }));
        Assert.Equal(0f, EmbeddingMath.CosineSimilarity(Array.Empty<float>(), new float[] { 1f }));
        Assert.Equal(0f, EmbeddingMath.CosineSimilarity(new float[] { 1f }, Array.Empty<float>()));
    }

    [Fact]
    public void EmbeddingSerializer_roundtrip()
    {
        var original = new float[] { 0.1f, -0.25f, 0.5f, 1f };

        var json = EmbeddingSerializer.ToJson(original);
        var restored = EmbeddingSerializer.FromJson(json);

        Assert.Equal(original.Length, restored.Length);
        for (var i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i], restored[i], precision: 5);
        }
    }
}
