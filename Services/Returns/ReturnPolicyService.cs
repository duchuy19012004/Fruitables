using Fruitables.Data;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class ReturnPolicyService : IReturnPolicyService
{
    private readonly ApplicationDbContext _db;
    public ReturnPolicyService(ApplicationDbContext db) => _db = db;

    public async Task<ReturnPolicy?> ResolveAsync(int productId, ReturnReasonCode reason, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var categoryId = await _db.Products.Where(x => x.Id == productId).Select(x => (int?)x.CategoryId).SingleOrDefaultAsync(cancellationToken);
        return await _db.ReturnPolicies.AsNoTracking()
            .Where(x => x.IsActive && x.Reason == reason && x.EffectiveFromUtc <= utcNow && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow))
            .Where(x => (x.Scope == ReturnPolicyScope.Product && x.ProductId == productId) ||
                        (x.Scope == ReturnPolicyScope.Category && x.CategoryId == categoryId) ||
                        x.Scope == ReturnPolicyScope.Default)
            .OrderByDescending(x => x.Scope == ReturnPolicyScope.Product ? 3 : x.Scope == ReturnPolicyScope.Category ? 2 : 1)
            .ThenByDescending(x => x.Version)
            .ThenByDescending(x => x.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
