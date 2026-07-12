// Services/Search/SearchTextNormalizer.cs
using System.Globalization;
using System.Text;

namespace Fruitables.Services.Search;

/// <summary>
/// Canonical form for typeahead match: trim, lower, strip Vietnamese diacritics, collapse spaces.
/// </summary>
public static class SearchTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var formD = text.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            // Vietnamese đ/Đ are not decomposed by FormD alone
            if (ch is 'đ' or 'Đ')
            {
                sb.Append('d');
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                    sb.Append(' ');
                continue;
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Trim();
    }
}
