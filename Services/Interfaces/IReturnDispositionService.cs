using Fruitables.Models.Returns;

namespace Fruitables.Services.Interfaces;

public interface IReturnDispositionService
{
    Task<(bool Success, string? Error, InventoryDisposition? Disposition)> RecordAsync(int returnItemId, int quantity, InventoryDispositionType disposition, int inspectorId, string notes, bool canOverridePolicy, CancellationToken cancellationToken = default);
}
