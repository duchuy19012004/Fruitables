using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface ISalesAnalyticsService
{
    Task<SalesHubVm> GetHubAsync(SalesAnalyticsFilterVm filter);
    Task<byte[]> ExportExcelAsync(SalesAnalyticsFilterVm filter);
}
