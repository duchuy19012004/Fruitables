using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Chat.Providers;
using Xunit;

namespace Fruitables.Tests.Chat;

public class RetrievalTextTests
{
    [Fact]
    public void QueryCoverage_phi_ship_matches_shipping_faq()
    {
        const string query = "Ph├¡ ship?";
        const string doc =
            "Ph├¡ vß║¡n chuyß╗ân nh╞░ thß║┐ n├áo?\n\n" +
            "Ph├¡ vß║¡n chuyß╗ân ─æ╞░ß╗úc t├¡nh theo khu vß╗▒c.\n\n" +
            "Tß╗½ kh├│a: ph├¡ ship ph├¡ vß║¡n chuyß╗ân giao h├áng shipping free ship COD";

        var score = RetrievalText.QueryCoverage(query, doc);

        Assert.True(score >= 0.35f, $"Expected coverage >= 0.35, got {score:F3}");
    }

    [Fact]
    public void QueryCoverage_weather_low_on_shipping_faq()
    {
        const string query = "Thß╗¥i tiß║┐t H├á Nß╗Öi ng├áy mai thß║┐ n├áo?";
        const string doc =
            "Ph├¡ vß║¡n chuyß╗ân nh╞░ thß║┐ n├áo?\n\nPh├¡ vß║¡n chuyß╗ân ─æ╞░ß╗úc t├¡nh theo khu vß╗▒c.";

        var score = RetrievalText.QueryCoverage(query, doc);

        Assert.True(score < 0.32f, $"Expected low coverage for OOD, got {score:F3}");
    }

    [Fact]
    public async Task LocalHash_similar_topics_score_higher_than_unrelated()
    {
        var client = new LocalHashEmbeddingClient(
            Microsoft.Extensions.Options.Options.Create(new Options.ChatOptions { EmbeddingDimensions = 256 }));

        var shipDoc = await client.EmbedAsync(
            "Ph├¡ vß║¡n chuyß╗ân\n\nNß╗Öi th├ánh zone 1. Tß╗½ kh├│a: ph├¡ ship shipping giao h├áng");
        var shipQ = await client.EmbedAsync("Ph├¡ ship bao nhi├¬u?");
        var weatherQ = await client.EmbedAsync("Thß╗¥i tiß║┐t H├á Nß╗Öi ng├áy mai?");

        var shipScore = EmbeddingMath.CosineSimilarity(shipQ, shipDoc);
        var weatherScore = EmbeddingMath.CosineSimilarity(weatherQ, shipDoc);

        Assert.True(shipScore > weatherScore,
            $"ship={shipScore:F3} should beat weather={weatherScore:F3}");
    }
}
