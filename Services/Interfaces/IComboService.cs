using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface IComboService
{
    Task<IReadOnlyList<ComboListRowViewModel>> GetAdminListAsync();
    Task<ComboFormViewModel?> GetForEditAsync(int id);
    Task<ComboResult> CreateAsync(ComboFormViewModel model);
    Task<ComboResult> UpdateAsync(int id, ComboFormViewModel model);
    Task<ComboResult> DeleteAsync(int id);
    Task<IReadOnlyList<ComboProductOptionViewModel>> GetProductOptionsAsync();
    Task<IReadOnlyList<ComboCardViewModel>> GetActiveComboCardsAsync();
    Task<AddComboToCartResult> AddComboToCartAsync(string sessionId, int comboId, ICartService cartService);
}
