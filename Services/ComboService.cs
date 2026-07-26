using System.Text.RegularExpressions;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services;

public class ComboService : IComboService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductPricingService _pricing;

    public ComboService(IUnitOfWork unitOfWork, IProductPricingService pricing)
    {
        _unitOfWork = unitOfWork;
        _pricing = pricing;
    }

    public async Task<IReadOnlyList<ComboListRowViewModel>> GetAdminListAsync()
    {
        var combos = await _unitOfWork.Combos.Query()
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .OrderBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();

        var result = new List<ComboListRowViewModel>();
        foreach (var combo in combos)
        {
            var card = await BuildCardAsync(combo);
            result.Add(new ComboListRowViewModel
            {
                Id = combo.Id,
                Name = combo.Name,
                ImageUrl = combo.ImageUrl,
                ItemCount = combo.Items.Count,
                TotalPrice = card?.TotalPrice ?? 0,
                IsActive = combo.IsActive
            });
        }
        return result;
    }

    public async Task<ComboFormViewModel?> GetForEditAsync(int id)
    {
        var combo = await _unitOfWork.Combos.Query()
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .Include(c => c.Items)
            .ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (combo == null) return null;

        var products = await GetProductOptionsAsync();

        return new ComboFormViewModel
        {
            Id = combo.Id,
            Name = combo.Name,
            Slug = combo.Slug,
            Description = combo.Description,
            ImageUrl = combo.ImageUrl,
            IsActive = combo.IsActive,
            SortOrder = combo.SortOrder,
            Items = combo.Items.OrderBy(i => i.SortOrder).Select(i => new ComboItemFormModel
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity,
                SortOrder = i.SortOrder
            }).ToList(),
            Products = products.ToList()
        };
    }

    public async Task<ComboResult> CreateAsync(ComboFormViewModel model)
    {
        var slug = await ResolveSlugAsync(model.Slug, model.Name);
        if (slug == null)
            return ComboResult.Fail("Slug đã tồn tại hoặc không hợp lệ.");

        var combo = new Combo
        {
            Name = model.Name.Trim(),
            Slug = slug,
            Description = model.Description?.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
            IsActive = model.IsActive,
            SortOrder = model.SortOrder,
            Items = BuildItems(model.Items)
        };

        await _unitOfWork.Combos.AddAsync(combo);
        await _unitOfWork.SaveChangesAsync();
        return ComboResult.Ok(combo);
    }

    public async Task<ComboResult> UpdateAsync(int id, ComboFormViewModel model)
    {
        var combo = await _unitOfWork.Combos.Query()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (combo == null)
            return ComboResult.Fail("Không tìm thấy combo.");

        var slug = await ResolveSlugAsync(model.Slug, model.Name, id);
        if (slug == null)
            return ComboResult.Fail("Slug đã tồn tại hoặc không hợp lệ.");

        combo.Name = model.Name.Trim();
        combo.Slug = slug;
        combo.Description = model.Description?.Trim();
        combo.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim();
        combo.IsActive = model.IsActive;
        combo.SortOrder = model.SortOrder;
        combo.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ComboItems.RemoveRange(combo.Items);
        combo.Items = BuildItems(model.Items);

        _unitOfWork.Combos.Update(combo);
        await _unitOfWork.SaveChangesAsync();
        return ComboResult.Ok(combo);
    }

    public async Task<ComboResult> DeleteAsync(int id)
    {
        var combo = await _unitOfWork.Combos.GetByIdAsync(id);
        if (combo == null)
            return ComboResult.Fail("Không tìm thấy combo.");

        _unitOfWork.Combos.Remove(combo);
        await _unitOfWork.SaveChangesAsync();
        return ComboResult.Ok();
    }

    public async Task<IReadOnlyList<ComboProductOptionViewModel>> GetProductOptionsAsync()
    {
        var products = await _unitOfWork.Products.Query()
            .Where(p => p.IsActive && !p.IsDeleted)
            .Include(p => p.Variants)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.Select(p => new ComboProductOptionViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Variants = p.Variants
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .Select(v => new ComboVariantOptionViewModel { Id = v.Id, Name = v.Name })
                .ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<ComboCardViewModel>> GetActiveComboCardsAsync()
    {
        var combos = await _unitOfWork.Combos.Query()
            .Where(c => c.IsActive)
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Images)
            .Include(c => c.Items)
            .ThenInclude(i => i.ProductVariant)
            .OrderBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();

        var cards = new List<ComboCardViewModel>();
        foreach (var combo in combos)
        {
            var card = await BuildCardAsync(combo);
            if (card != null && card.Items.Any(i => i.IsAvailable))
                cards.Add(card);
        }
        return cards;
    }

    public async Task<AddComboToCartResult> AddComboToCartAsync(string sessionId, int comboId, ICartService cartService)
    {
        var combo = await _unitOfWork.Combos.Query()
            .Where(c => c.IsActive)
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .Include(c => c.Items)
            .ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(c => c.Id == comboId);

        if (combo == null)
            return new AddComboToCartResult { Success = false, Message = "Không tìm thấy combo." };

        var targets = combo.Items
            .Select(i => new PriceTargetKey(i.ProductId, i.ProductVariantId))
            .Distinct()
            .ToList();

        var quotes = targets.Any()
            ? await _pricing.GetQuotesAsync(targets)
            : new Dictionary<PriceTargetKey, PriceQuote>();

        var added = 0;
        var skipped = new List<string>();

        foreach (var item in combo.Items.OrderBy(i => i.SortOrder))
        {
            var reason = GetUnavailableReason(item, quotes);
            if (!string.IsNullOrEmpty(reason))
            {
                skipped.Add($"{item.Product.Name} ({reason})");
                continue;
            }

            var cartResult = await cartService.AddToCartAsync(
                sessionId,
                item.ProductId,
                item.Quantity,
                item.ProductVariantId);

            if (!cartResult.Success)
            {
                skipped.Add($"{item.Product.Name} ({cartResult.Message})");
                continue;
            }

            added++;
        }

        var message = added > 0
            ? $"Đã thêm {added} món từ combo '{combo.Name}' vào giỏ hàng."
            : $"Không thể thêm món nào từ combo '{combo.Name}'.";

        if (skipped.Any())
            message += " Bỏ qua: " + string.Join(", ", skipped) + ".";

        return new AddComboToCartResult
        {
            Success = added > 0,
            AddedCount = added,
            SkippedItems = skipped,
            Message = message
        };
    }

    private async Task<ComboCardViewModel?> BuildCardAsync(Combo combo)
    {
        if (combo.Items == null) return null;

        var targets = combo.Items
            .Select(i => new PriceTargetKey(i.ProductId, i.ProductVariantId))
            .Distinct()
            .ToList();

        var quotes = targets.Any()
            ? await _pricing.GetQuotesAsync(targets)
            : new Dictionary<PriceTargetKey, PriceQuote>();

        var items = combo.Items
            .OrderBy(i => i.SortOrder)
            .Select(i =>
            {
                var key = new PriceTargetKey(i.ProductId, i.ProductVariantId);
                var reason = GetUnavailableReason(i, quotes);
                var available = string.IsNullOrEmpty(reason);
                var quote = available && quotes.TryGetValue(key, out var q) ? q : null;

                return new ComboCardItemViewModel
                {
                    ProductId = i.ProductId,
                    ProductVariantId = i.ProductVariantId,
                    ProductName = i.Product?.Name ?? "[Đã xóa]",
                    ProductImage = i.Product?.Images?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                                   ?? i.Product?.Images?.FirstOrDefault()?.ImageUrl,
                    VariantName = i.ProductVariant?.Name,
                    Quantity = i.Quantity,
                    UnitPrice = quote?.EffectivePrice ?? 0,
                    IsAvailable = available,
                    UnavailableReason = reason
                };
            })
            .ToList();

        return new ComboCardViewModel
        {
            Id = combo.Id,
            Name = combo.Name,
            Slug = combo.Slug,
            Description = combo.Description,
            ImageUrl = combo.ImageUrl,
            TotalPrice = items.Where(i => i.IsAvailable).Sum(i => i.UnitPrice * i.Quantity),
            Items = items
        };
    }

    private string GetUnavailableReason(ComboItem item, IReadOnlyDictionary<PriceTargetKey, PriceQuote> quotes)
    {
        var key = new PriceTargetKey(item.ProductId, item.ProductVariantId);
        if (!quotes.ContainsKey(key))
            return "tạm hết";

        var stock = item.ProductVariant?.StockQuantity ?? item.Product?.StockQuantity ?? 0;
        if (item.Quantity > stock)
            return "không đủ tồn kho";

        return string.Empty;
    }

    private List<ComboItem> BuildItems(List<ComboItemFormModel> items)
    {
        return items
            .Where(i => i.ProductId > 0)
            .OrderBy(i => i.SortOrder)
            .Select((i, idx) => new ComboItem
            {
                ProductId = i.ProductId,
                ProductVariantId = i.ProductVariantId,
                Quantity = Math.Max(1, i.Quantity),
                SortOrder = i.SortOrder == 0 ? idx : i.SortOrder
            })
            .ToList();
    }

    private async Task<string?> ResolveSlugAsync(string? requestedSlug, string name, int? excludeId = null)
    {
        var slug = string.IsNullOrWhiteSpace(requestedSlug) ? GenerateSlug(name) : GenerateSlug(requestedSlug);
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var existing = await _unitOfWork.Combos.Query()
            .Where(c => c.Slug == slug && (!excludeId.HasValue || c.Id != excludeId.Value))
            .FirstOrDefaultAsync();

        return existing == null ? slug : null;
    }

    private static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var slug = name.ToLowerInvariant();
        slug = slug.Replace("đ", "d").Replace("Đ", "d");
        slug = Regex.Replace(slug, "[àáạảãâầấậẩẫăằắặẳẵ]", "a");
        slug = Regex.Replace(slug, "[èéẹẻẽêềếệểễ]", "e");
        slug = Regex.Replace(slug, "[ìíịỉĩ]", "i");
        slug = Regex.Replace(slug, "[òóọỏõôồốộổỗơờớợởỡ]", "o");
        slug = Regex.Replace(slug, "[ùúụủũưừứựửữ]", "u");
        slug = Regex.Replace(slug, "[ỳýỵỷỹ]", "y");
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "combo" : slug;
    }
}
