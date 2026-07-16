using Fruitables.Services.Chat;
using Xunit;

namespace Fruitables.Tests.Chat;

public class SpaceXaiClientParsingTests
{
    [Fact]
    public void ParseChatCompletionContent_extracts_message_content()
    {
        const string json = """
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "Xin chào từ Fruitables!"
                  },
                  "finish_reason": "stop"
                }
              ]
            }
            """;

        var content = SpaceXaiResponseParser.ParseChatCompletionContent(json);

        Assert.Equal("Xin chào từ Fruitables!", content);
    }

    [Fact]
    public void ParseChatCompletionContent_allows_empty_string_content()
    {
        const string json = """
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": ""
                  }
                }
              ]
            }
            """;

        var content = SpaceXaiResponseParser.ParseChatCompletionContent(json);

        Assert.Equal(string.Empty, content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("""{"choices":[]}""")]
    [InlineData("""{"choices":[{"message":{}}]}""")]
    [InlineData("""{"choices":[{"message":{"content":null}}]}""")]
    [InlineData("""{"choices":[{"message":{"content":123}}]}""")]
    public void ParseChatCompletionContent_throws_on_unexpected_payload(string json)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SpaceXaiResponseParser.ParseChatCompletionContent(json));

        Assert.Contains("Unexpected", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseStreamDeltaContent_extracts_delta_text()
    {
        const string json = """
            {
              "id": "chatcmpl-1",
              "object": "chat.completion.chunk",
              "choices": [
                {
                  "index": 0,
                  "delta": { "content": "Xin " },
                  "finish_reason": null
                }
              ]
            }
            """;

        var delta = SpaceXaiResponseParser.TryParseStreamDeltaContent(json);

        Assert.Equal("Xin ", delta);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[DONE]")]
    [InlineData("""{"choices":[{"delta":{"role":"assistant"}}]}""")]
    [InlineData("""{"choices":[{"delta":{}}]}""")]
    [InlineData("{}")]
    public void TryParseStreamDeltaContent_returns_null_when_no_content(string json)
    {
        Assert.Null(SpaceXaiResponseParser.TryParseStreamDeltaContent(json));
    }

    [Fact]
    public void ParseEmbedding_extracts_float_array()
    {
        const string json = """
            {
              "object": "list",
              "data": [
                {
                  "object": "embedding",
                  "index": 0,
                  "embedding": [0.1, -0.25, 0.5, 1.0]
                }
              ],
              "model": "text-embedding-3-small"
            }
            """;

        var vector = SpaceXaiResponseParser.ParseEmbedding(json);

        Assert.Equal(4, vector.Length);
        Assert.Equal(0.1f, vector[0], precision: 5);
        Assert.Equal(-0.25f, vector[1], precision: 5);
        Assert.Equal(0.5f, vector[2], precision: 5);
        Assert.Equal(1.0f, vector[3], precision: 5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("""{"data":[]}""")]
    [InlineData("""{"data":[{"embedding":"oops"}]}""")]
    [InlineData("""{"data":[{}]}""")]
    [InlineData("""{"data":[{"embedding":[true]}]}""")]
    public void ParseEmbedding_throws_on_unexpected_payload(string json)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SpaceXaiResponseParser.ParseEmbedding(json));

        Assert.Contains("Unexpected", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
