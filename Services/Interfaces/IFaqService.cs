using Fruitables.Models;

namespace Fruitables.Services.Interfaces;

public interface IFaqService
{
    Task<List<Faq>> GetAllAsync(CancellationToken ct = default);

    Task<Faq?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Faq> CreateAsync(string title, string body, string category, bool isActive, CancellationToken ct = default);

    Task<Faq?> UpdateAsync(int id, string title, string body, string category, bool isActive, CancellationToken ct = default);

    Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default);

    Task ReindexAllAsync(CancellationToken ct = default);
}
