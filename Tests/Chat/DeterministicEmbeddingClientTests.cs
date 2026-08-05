using Fruitables.Services.Chat.Providers;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Tests.Chat.Fakes;
using Xunit;

namespace Fruitables.Tests.Chat;

public class DeterministicEmbeddingClientTests
{
    [Fact]
    public async Task EmbedAsync_is_deterministic_and_unit_length()
    {
        var client = new DeterministicEmbeddingClient(dimensions: 32);

        var a = await client.EmbedAsync("phí ship nội thành");
        var b = await client.EmbedAsync("phí ship nội thành");

        Assert.Equal(32, a.Length);
        Assert.Equal(a, b);

        var norm = Math.Sqrt(a.Sum(x => x * x));
        Assert.Equal(1.0, norm, precision: 5);
    }

    [Fact]
    public async Task Similar_strings_score_higher_than_unrelated()
    {
        var client = new DeterministicEmbeddingClient(dimensions: 32);

        var query = await client.EmbedAsync("chính sách hỗ trợ đơn hàng");
        var related = await client.EmbedAsync("hỗ trợ đơn hàng trong 7 ngày");
        var unrelated = await client.EmbedAsync("cách nấu canh bí đỏ");

        var simRelated = EmbeddingMath.CosineSimilarity(query, related);
        var simUnrelated = EmbeddingMath.CosineSimilarity(query, unrelated);

        Assert.True(simRelated > simUnrelated, $"Expected related ({simRelated}) > unrelated ({simUnrelated})");
    }

    [Fact]
    public async Task FakeLlmClient_records_calls_and_returns_response()
    {
        var llm = new FakeLlmClient { Response = "OK" };

        var answer = await llm.CompleteAsync("sys", "user");

        Assert.Equal("OK", answer);
        Assert.Single(llm.Calls);
        Assert.Equal(("sys", "user"), llm.Calls[0]);
    }
}
