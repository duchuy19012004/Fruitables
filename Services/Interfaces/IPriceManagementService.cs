using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface IPriceManagementService
{
    Task<PriceManagementViewModel> GetDashboardAsync(string? search = null, string? filter = null);
    Task<PriceManagementResult> CreateScheduleAsync(SavePriceScheduleRequest request, int adminId);
    Task<PriceManagementResult> UpdateScheduleAsync(int id, SavePriceScheduleRequest request, int adminId);
    Task<PriceManagementResult> CancelScheduleAsync(int id, int adminId);
    Task<PriceManagementResult> UpdateBasePriceAsync(PriceTargetKey target, decimal newPrice, int adminId);
    Task<PriceManagementResult> BulkUpdateBasePricesAsync(BulkPriceUpdateRequest request, int adminId);
}
