namespace Fruitables.Services;

// Tiện ích escape giá trị cho một ô CSV.
public static class CsvEscaper
{
    /// <summary>
    /// Escape giá trị cho ô CSV: quote khi chứa dấu phân cách / xuống dòng,
    /// và chặn formula injection (ô bắt đầu bằng = + - @ hoặc tab/CR) bằng cách
    /// prefix dấu nháy đơn để Excel/Sheets coi là chuỗi, không thực thi công thức.
    /// </summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var first = value[0];
        var sanitized = first is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? "'" + value
            : value;

        var escaped = sanitized.Replace("\"", "\"\"");
        return sanitized.Contains(',') || sanitized.Contains('"') || sanitized.Contains('\n') || sanitized.Contains('\r')
            ? $"\"{escaped}\""
            : escaped;
    }
}
