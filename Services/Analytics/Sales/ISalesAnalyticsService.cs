using Fruitables.ViewModels;

namespace Fruitables.Services.Analytics.Sales;

public interface ISalesAnalyticsService
{
    Task<SalesHubVm> GetHubAsync(SalesAnalyticsFilterVm filter);
    Task<byte[]> ExportExcelAsync(SalesAnalyticsFilterVm filter);
}
