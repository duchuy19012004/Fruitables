using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models.Returns;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class ReturnPolicyVersionCommand
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    public ReturnPolicyVersionCommand(ApplicationDbContext db, TimeProvider clock) { _db = db; _clock = clock; }

    public async Task<ReturnPolicy> CreateFromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var input = JsonSerializer.Deserialize<ReturnPolicy>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Policy JSON không hợp lệ.");
        if (string.IsNullOrWhiteSpace(input.Name) || input.ClaimWindowHours <= 0) throw new InvalidOperationException("Name và ClaimWindowHours là bắt buộc.");
        if (input.Scope == ReturnPolicyScope.Product && input.ProductId == null || input.Scope == ReturnPolicyScope.Category && input.CategoryId == null) throw new InvalidOperationException("Policy scope thiếu ProductId hoặc CategoryId.");
        var versions = await _db.ReturnPolicies.Where(x => x.Scope == input.Scope && x.ProductId == input.ProductId && x.CategoryId == input.CategoryId && x.Reason == input.Reason).Select(x => x.Version).ToListAsync(cancellationToken);
        input.Id = 0;
        input.Version = versions.Count == 0 ? 1 : versions.Max() + 1;
        input.IsActive = true;
        input.EffectiveFromUtc = input.EffectiveFromUtc == default ? _clock.GetUtcNow().UtcDateTime : input.EffectiveFromUtc.ToUniversalTime();
        input.CreatedAtUtc = _clock.GetUtcNow().UtcDateTime;
        input.Category = null;
        input.Product = null;
        _db.ReturnPolicies.Add(input);
        await _db.SaveChangesAsync(cancellationToken);
        return input;
    }
}
