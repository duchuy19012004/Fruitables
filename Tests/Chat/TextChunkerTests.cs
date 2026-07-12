using Fruitables.Services.Chat;
using Xunit;

namespace Fruitables.Tests.Chat;

public class TextChunkerTests
{
    [Fact]
    public void Chunk_short_text_returns_single_chunk()
    {
        var text = "Hello world";

        var chunks = TextChunker.Chunk(text);

        Assert.Single(chunks);
        Assert.Equal("Hello world", chunks[0]);
    }

    [Fact]
    public void Chunk_empty_or_whitespace_returns_empty_list()
    {
        Assert.Empty(TextChunker.Chunk(""));
        Assert.Empty(TextChunker.Chunk("   "));
        Assert.Empty(TextChunker.Chunk(null!));
    }

    [Fact]
    public void Chunk_long_text_uses_sliding_window_with_overlap()
    {
        var text = new string('a', 1200);
        const int maxChars = 500;
        const int overlapChars = 50;

        var chunks = TextChunker.Chunk(text, maxChars, overlapChars);

        Assert.True(chunks.Count >= 2, $"Expected at least 2 chunks, got {chunks.Count}");
        Assert.All(chunks, c => Assert.True(c.Length <= maxChars, $"Chunk length {c.Length} exceeds {maxChars}"));

        // Reassembled coverage: first char of each chunk should land on the sliding step.
        Assert.Equal(maxChars, chunks[0].Length);
        Assert.Equal(new string('a', maxChars), chunks[0]);
    }

    [Fact]
    public void Chunk_trims_input()
    {
        var chunks = TextChunker.Chunk("  short  ");

        Assert.Single(chunks);
        Assert.Equal("short", chunks[0]);
    }
}
