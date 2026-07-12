namespace Fruitables.Services.Chat;

/// <summary>
/// Splits text into overlapping character windows for RAG indexing.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Chunks <paramref name="text"/> into segments of at most <paramref name="maxChars"/> characters,
    /// advancing by <c>maxChars - overlapChars</c> so consecutive windows share <paramref name="overlapChars"/> characters.
    /// </summary>
    public static List<string> Chunk(string text, int maxChars = 1200, int overlapChars = 150)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        text = text.Trim();

        if (text.Length <= maxChars)
        {
            return new List<string> { text };
        }

        // Guard against non-progress when overlap >= maxChars.
        var step = Math.Max(1, maxChars - Math.Max(0, overlapChars));
        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var length = Math.Min(maxChars, text.Length - start);
            chunks.Add(text.Substring(start, length));

            if (start + length >= text.Length)
            {
                break;
            }

            start += step;
        }

        return chunks;
    }
}
