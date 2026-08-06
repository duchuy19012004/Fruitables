namespace Fruitables.Services.Infrastructure.DatabaseConsolidation;

public static class DatabaseConsolidationSql
{
    public static string BuildIsJsonQuery(
        string tableName,
        IReadOnlyCollection<string> columns,
        bool nullable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
            throw new ArgumentException("At least one JSON column is required.", nameof(columns));

        var table = QuoteIdentifier(tableName);
        var predicates = columns.Select(column =>
        {
            var quotedColumn = QuoteIdentifier(column);
            return nullable
                ? $"({quotedColumn} IS NOT NULL AND ISJSON({quotedColumn}) <> 1)"
                : $"ISJSON({quotedColumn}) <> 1";
        });

        return $"SELECT CONVERT(nvarchar(100), [Id]) AS [Value] FROM {table} WHERE {string.Join(" OR ", predicates)};";
    }

    private static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        if (identifier.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("SQL identifiers may contain only letters, digits, and underscores.", nameof(identifier));

        return $"[{identifier}]";
    }
}
