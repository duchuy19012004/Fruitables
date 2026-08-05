namespace Fruitables.Services.Chat.Knowledge;

// ============================================================
// Cắt văn bản dài thành nhiều đoạn ngắn hơn.
//
// Vì sao cần?
// - AI/tìm kiếm làm việc tốt hơn với đoạn vừa phải, không nuốt cả cuốn sách một lần.
// - overlap = phần chồng lấn giữa 2 đoạn, để không "cắt đứt" ý nghĩa ở giữa câu.
// ============================================================
public static class TextChunker
{
    // maxChars: độ dài tối đa 1 đoạn
    // overlapChars: số ký tự lặp lại từ đoạn trước sang đoạn sau
    public static List<string> Chunk(string text, int maxChars = 1200, int overlapChars = 150)
    {
        // Bỏ khoảng trắng thừa 2 đầu
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
            return new List<string>();

        // Ngắn thì không cần cắt
        if (text.Length <= maxChars)
            return new List<string> { text };

        var result = new List<string>();
        var start = 0;

        // Trượt cửa sổ dọc theo đoạn văn
        while (start < text.Length)
        {
            var len = Math.Min(maxChars, text.Length - start);
            result.Add(text.Substring(start, len));

            if (start + len >= text.Length)
                break;

            // Bước nhảy = max - overlap (có phần chồng)
            start += Math.Max(1, maxChars - overlapChars);
        }

        return result;
    }
}
