namespace Fruitables.Services.Infrastructure.DatabaseConsolidation;

public interface IDatabaseConsolidationService
{
    Task<ConsolidationReport> BackfillAsync(bool apply, CancellationToken cancellationToken);

    Task<ConsolidationVerificationReport> VerifyAsync(CancellationToken cancellationToken);
}
