namespace Fruitables.Services.Infrastructure.DatabaseConsolidation;

public sealed class ConsolidationVerificationReport
{
    private readonly List<ConsolidationError> _errors = [];

    public bool Success => _errors.Count == 0;

    public bool IsValid => Success;

    public bool IsJsonValid { get; internal set; } = true;

    public IReadOnlyDictionary<string, int> SourceCounts => _sourceCounts;

    public IReadOnlyDictionary<string, int> TargetCounts => _targetCounts;

    public IReadOnlyList<ConsolidationError> Errors => _errors;

    public IReadOnlyList<string> FailedSourceIds =>
        _errors.Select(error => error.SourceId).Distinct(StringComparer.Ordinal).ToArray();

    internal Dictionary<string, int> MutableSourceCounts => _sourceCounts;

    internal Dictionary<string, int> MutableTargetCounts => _targetCounts;

    internal void AddError(
        string aggregateType,
        string sourceId,
        string message,
        string? exceptionType = null)
    {
        _errors.Add(new ConsolidationError(aggregateType, sourceId, message, exceptionType));
    }

    private readonly Dictionary<string, int> _sourceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _targetCounts = new(StringComparer.OrdinalIgnoreCase);
}
