namespace Fruitables.Services.Infrastructure.DatabaseConsolidation;

public sealed record ConsolidationError(
    string AggregateType,
    string SourceId,
    string Message,
    string? ExceptionType = null);

public sealed class ConsolidationReport
{
    private readonly List<ConsolidationError> _errors = [];

    internal ConsolidationReport(bool applied) => Applied = applied;

    public bool Applied { get; }

    public bool DryRun => !Applied;

    public int Planned { get; internal set; }

    public int Processed { get; internal set; }

    public int Skipped { get; internal set; }

    public int Failed { get; internal set; }

    public int ErrorCount => Failed;

    public bool Success => Failed == 0;

    public bool HasErrors => !Success;

    public IReadOnlyList<ConsolidationError> Errors => _errors;

    public IReadOnlyList<string> FailedSourceIds =>
        _errors.Select(error => error.SourceId).Distinct(StringComparer.Ordinal).ToArray();

    internal void AddError(ConsolidationError error)
    {
        _errors.Add(error);
        Failed++;
    }
}
