using System.Text.RegularExpressions;

namespace Fruitables.Services.Reviews;

/// <summary>
/// Service để che các từ ngữ không phù hợp trong nội dung đánh giá
/// </summary>
public class WordMaskingService : IWordMaskingService
{
    private readonly List<string> _bannedWords;

    public WordMaskingService()
    {
        // Danh sách từ cấm mặc định (trong thực tế sẽ load từ database)
        _bannedWords = new List<string> { "xấu", "tệ", "dở", "chán", "spam" };
    }

    public WordMaskingService(List<string> bannedWords)
    {
        _bannedWords = bannedWords ?? new List<string>();
    }

    /// <summary>
    /// Che các từ không phù hợp trong nội dung bằng ký tự ***
    /// </summary>
    /// <param name="content">Nội dung gốc</param>
    /// <returns>Nội dung đã được che từ không phù hợp</returns>
    public string MaskContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var result = content;
        
        foreach (var word in _bannedWords)
        {
            // Sử dụng regex để thay thế từ (case-insensitive)
            var pattern = $@"\b{Regex.Escape(word)}\b";
            result = Regex.Replace(result, pattern, "***", RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Kiểm tra nội dung có chứa từ không phù hợp không
    /// </summary>
    public bool ContainsBannedWords(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        foreach (var word in _bannedWords)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Lấy danh sách các từ bị che trong nội dung
    /// </summary>
    public List<string> GetMaskedWords(string content)
    {
        var maskedWords = new List<string>();
        
        if (string.IsNullOrEmpty(content))
            return maskedWords;

        foreach (var word in _bannedWords)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                maskedWords.Add(word);
        }

        return maskedWords;
    }
}

public interface IWordMaskingService
{
    string MaskContent(string content);
    bool ContainsBannedWords(string content);
    List<string> GetMaskedWords(string content);
}
