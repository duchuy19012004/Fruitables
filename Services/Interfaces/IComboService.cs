using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface IComboService
{
    Task<IReadOnlyList<ComboListRowViewModel>> GetAdminListAsync();
    Task<ComboFormViewModel?> GetForEditAsync(int id);
    Task<ComboResult> CreateAsync(ComboFormViewModel model, int? adminId = null);
    Task<ComboResult> UpdateAsync(int id, ComboFormViewModel model, int? adminId = null);
    Task<ComboResult> DeleteAsync(int id, int? adminId = null);
    Task<IReadOnlyList<ComboProductOptionViewModel>> GetProductOptionsAsync();
    Task<IReadOnlyList<ComboCardViewModel>> GetActiveComboCardsAsync();
    Task<ComboReportViewModel> GetReportAsync(DateTime from, DateTime to);
    Task<ComboAuditViewModel?> GetAuditAsync(int comboId, int take = 100);
    Task<AddComboToCartResult> AddComboToCartAsync(string sessionId, int comboId, ICartService cartService);
}
