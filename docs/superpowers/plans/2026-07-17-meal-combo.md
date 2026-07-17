# Meal Combo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an admin-managed "meal combo" feature so customers can add all ingredients for a dish to their cart in one click from the Shop page.

**Architecture:** Two new entities (`Combo`, `ComboItem`) are persisted via EF Core. A new `IComboService` handles admin CRUD, storefront card rendering (real-time prices via `IProductPricingService`), and adding combos to the cart by reusing `ICartService.AddToCartAsync`. Admin UI follows the existing Category CRUD pattern; storefront UI is a new section on the existing Shop page.

**Tech Stack:** ASP.NET Core MVC (.NET 8), Entity Framework Core, SQL Server, Bootstrap 5 (existing admin/storefront CSS).

## Global Constraints

- Feature scope is strictly: admin CRUD for combos + storefront section + one-click add-to-cart. No combo-level discounts, no AI recommendations, no standalone combo detail page.
- Project has no test project — do not add one. Verify each task with `dotnet build` and the final task with manual browser checks (Playwright MCP is available).
- Reuse existing services: `ICartService`, `IProductPricingService`, `IUnitOfWork`, `RequirePermissionFilter` is applied globally via `Program.cs` so admin controllers only need `[Authorize(Roles = "Admin,SuperAdmin")]`.
- All new identifiers are English; all user-facing Vietnamese labels match the tone of existing admin/storefront views.
- Every task ends with a commit. Keep commits small and focused.

---

## File Structure

| File | Responsibility |
|---|---|
| `Models/Combo.cs` | Entity: combo header (name, slug, image, active flag, sort order). |
| `Models/ComboItem.cs` | Entity: one ingredient line inside a combo (product, optional variant, quantity, sort order). |
| `Models/ComboResult.cs` | Result object returned by combo service operations. |
| `Data/ApplicationDbContext.cs` | DbSets + EF configuration for `Combo`/`ComboItem` (unique slug, cascade delete for items). |
| `Repositories/Interfaces/IUnitOfWork.cs` | Exposes `IRepository<Combo>` and `IRepository<ComboItem>`. |
| `Repositories/UnitOfWork.cs` | Lazy repository fields for `Combo`/`ComboItem`. |
| `ViewModels/ComboViewModels.cs` | All combo-related view models (admin forms, list rows, storefront cards, add-to-cart result). |
| `Services/Interfaces/IComboService.cs` | Service contract. |
| `Services/ComboService.cs` | All combo business logic. |
| `Program.cs` | Registers `IComboService`. |
| `Areas/Admin/Controllers/ComboController.cs` | Admin CRUD + variant lookup AJAX. |
| `Areas/Admin/Views/Combo/Index.cshtml` | Admin list. |
| `Areas/Admin/Views/Combo/_ComboForm.cshtml` | Shared Create/Edit form with dynamic item rows. |
| `Areas/Admin/Views/Combo/_ComboItemRow.cshtml` | Single ingredient row partial. |
| `Areas/Admin/Views/Combo/Create.cshtml` | Create wrapper. |
| `Areas/Admin/Views/Combo/Edit.cshtml` | Edit wrapper. |
| `Areas/Admin/Views/Shared/_AdminSidebar.cshtml` | Adds "Combo món ăn" menu item. |
| `Controllers/ComboController.cs` | Storefront `AddToCart` POST action. |
| `Views/Shop/_ComboSection.cshtml` | Renders combo cards on Shop page. |
| `Views/Shop/Index.cshtml` | Injects combo section between hero and product grid. |

---

### Task 1: Create entities and EF configuration

**Files:**
- Create: `Models/Combo.cs`
- Create: `Models/ComboItem.cs`
- Create: `Models/ComboResult.cs`
- Modify: `Data/ApplicationDbContext.cs`
- Modify: `Repositories/Interfaces/IUnitOfWork.cs`
- Modify: `Repositories/UnitOfWork.cs`

**Interfaces:**
- Produces: `Combo`, `ComboItem`, `ComboResult` types; `Combos`/`ComboItems` repositories; migration-ready EF config.

- [ ] **Step 1: Create `Models/Combo.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class Combo
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ComboItem> Items { get; set; } = new List<ComboItem>();
}
```

- [ ] **Step 2: Create `Models/ComboItem.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class ComboItem
{
    public int Id { get; set; }

    public int ComboId { get; set; }

    public int ProductId { get; set; }

    public int? ProductVariantId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;

    public virtual Combo Combo { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
}
```

- [ ] **Step 3: Create `Models/ComboResult.cs`**

```csharp
namespace Fruitables.Models;

public class ComboResult
{
    public bool Success { get; private set; }
    public Combo? Combo { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ComboResult() { }

    public static ComboResult Ok(Combo? combo = null)
        => new() { Success = true, Combo = combo };

    public static ComboResult Fail(string message)
        => new() { Success = false, ErrorMessage = message };
}
```

- [ ] **Step 4: Modify `Data/ApplicationDbContext.cs`**

Add these two `DbSet` properties after the existing `PriceSchedules` line:

```csharp
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<ComboItem> ComboItems => Set<ComboItem>();
```

Add this block inside `OnModelCreating` (for example, after the `ProductVariant` configuration):

```csharp
        // Combo
        modelBuilder.Entity<Combo>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // ComboItem
        modelBuilder.Entity<ComboItem>(entity =>
        {
            entity.HasIndex(e => new { e.ComboId, e.SortOrder });
            entity.HasOne(i => i.Combo)
                  .WithMany(c => c.Items)
                  .HasForeignKey(i => i.ComboId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.Product)
                  .WithMany()
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.ProductVariant)
                  .WithMany()
                  .HasForeignKey(i => i.ProductVariantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
```

- [ ] **Step 5: Modify `Repositories/Interfaces/IUnitOfWork.cs`**

Add these two properties in the interface:

```csharp
    IRepository<Combo> Combos { get; }
    IRepository<ComboItem> ComboItems { get; }
```

- [ ] **Step 6: Modify `Repositories/UnitOfWork.cs`**

Add private fields:

```csharp
    private IRepository<Combo>? _combos;
    private IRepository<ComboItem>? _comboItems;
```

Add public properties:

```csharp
    public IRepository<Combo> Combos =>
        _combos ??= new Repository<Combo>(_context);

    public IRepository<ComboItem> ComboItems =>
        _comboItems ??= new Repository<ComboItem>(_context);
```

- [ ] **Step 7: Create EF migration**

Run:

```bash
dotnet ef migrations add AddMealCombo --project Fruitables.csproj
```

Expected: a new migration file appears under `Migrations/` (timestamp prefix `AddMealCombo`).

- [ ] **Step 8: Apply migration to the local database**

Run:

```bash
dotnet ef database update --project Fruitables.csproj
```

Expected: migration applies successfully (SQL Server must be reachable).

- [ ] **Step 9: Build and commit**

Run:

```bash
dotnet build Fruitables.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

Commit:

```bash
git add Models/Combo.cs Models/ComboItem.cs Models/ComboResult.cs Data/ApplicationDbContext.cs Repositories/Interfaces/IUnitOfWork.cs Repositories/UnitOfWork.cs Migrations/
git commit -m "feat(combo): add Combo and ComboItem entities with migration"
```


### Task 2: Add combo view models

**Files:**
- Create: `ViewModels/ComboViewModels.cs`

**Interfaces:**
- Consumes: `Combo`, `ComboItem`, `ComboResult` from Task 1.
- Produces: `ComboFormViewModel`, `ComboListRowViewModel`, `ComboCardViewModel`, `AddComboToCartResult`, and supporting models used by Task 3/4/5.

- [ ] **Step 1: Create `ViewModels/ComboViewModels.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace Fruitables.ViewModels;

// ========== Admin form models ==========

public class ComboItemFormModel
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int? ProductVariantId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 1")]
    public int Quantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;
}

public class ComboFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên combo không được để trống")]
    [StringLength(255, ErrorMessage = "Tên combo không được vượt quá 255 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Slug không được vượt quá 255 ký tự")]
    public string? Slug { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Hình ảnh (URL)")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Thứ tự hiển thị")]
    public int SortOrder { get; set; } = 0;

    public List<ComboItemFormModel> Items { get; set; } = new();

    // Dropdown data for admin views
    public List<ComboProductOptionViewModel> Products { get; set; } = new();
}

public class CreateComboViewModel : ComboFormViewModel { }

public class EditComboViewModel : ComboFormViewModel { }

public class ComboProductOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ComboVariantOptionViewModel> Variants { get; set; } = new();
}

public class ComboVariantOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ========== Admin list models ==========

public class ComboListViewModel
{
    public List<ComboListRowViewModel> Items { get; set; } = new();
}

public class ComboListRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsActive { get; set; }
}

// ========== Storefront card models ==========

public class ComboCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal TotalPrice { get; set; }
    public List<ComboCardItemViewModel> Items { get; set; } = new();
}

public class ComboCardItemViewModel
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsAvailable { get; set; }
    public string UnavailableReason { get; set; } = string.Empty;
}

// ========== Add-to-cart result ==========

public class AddComboToCartResult
{
    public bool Success { get; set; }
    public int AddedCount { get; set; }
    public List<string> SkippedItems { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Build and commit**

Run:

```bash
dotnet build Fruitables.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

Commit:

```bash
git add ViewModels/ComboViewModels.cs
git commit -m "feat(combo): add combo view models"
```

---

### Task 3: Implement combo service and register DI

**Files:**
- Create: `Services/Interfaces/IComboService.cs`
- Create: `Services/ComboService.cs`
- Modify: `Program.cs`

**Interfaces:**
- Consumes: `IUnitOfWork`, `IProductPricingService`, `ICartService`, `Combo`, `ComboItem`, view models from Task 2.
- Produces: `IComboService` methods used by admin controller, storefront controller, and shop view.

- [ ] **Step 1: Create `Services/Interfaces/IComboService.cs`**

```csharp
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
```

- [ ] **Step 2: Create `Services/ComboService.cs`**

```csharp
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

            await cartService.AddToCartAsync(sessionId, item.ProductId, item.Quantity, item.ProductVariantId);
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
```

- [ ] **Step 3: Register service in `Program.cs`**

Add this line in the service registration block (for example, after `builder.Services.AddScoped<IProductAdminService, ProductAdminService>();`):

```csharp
builder.Services.AddScoped<IComboService, ComboService>();
```

- [ ] **Step 4: Build and commit**

Run:

```bash
dotnet build Fruitables.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

Commit:

```bash
git add Services/Interfaces/IComboService.cs Services/ComboService.cs Program.cs
git commit -m "feat(combo): add IComboService and ComboService"
```


### Task 4: Build admin CRUD UI

**Files:**
- Create: `Areas/Admin/Controllers/ComboController.cs`
- Create: `Areas/Admin/Views/Combo/Index.cshtml`
- Create: `Areas/Admin/Views/Combo/_ComboForm.cshtml`
- Create: `Areas/Admin/Views/Combo/_ComboItemRow.cshtml`
- Create: `Areas/Admin/Views/Combo/Create.cshtml`
- Create: `Areas/Admin/Views/Combo/Edit.cshtml`
- Modify: `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`

**Interfaces:**
- Consumes: `IComboService` and view models from Task 2.
- Produces: `/Admin/Combo/*` routes used by admin sidebar; `/Admin/Combo/GetVariants` used by the form JavaScript.

- [ ] **Step 1: Create `Areas/Admin/Controllers/ComboController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ComboController : Controller
{
    private readonly IComboService _comboService;

    public ComboController(IComboService comboService)
    {
        _comboService = comboService;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _comboService.GetAdminListAsync();
        return View(new ComboListViewModel { Items = items.ToList() });
    }

    public async Task<IActionResult> Create()
    {
        var products = await _comboService.GetProductOptionsAsync();
        return View(new CreateComboViewModel { Products = products.ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ComboFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        var result = await _comboService.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        TempData["Success"] = "Tạo combo món ăn thành công!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var model = await _comboService.GetForEditAsync(id);
        if (model == null)
        {
            TempData["Error"] = "Không tìm thấy combo";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ComboFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        var result = await _comboService.UpdateAsync(id, model);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        TempData["Success"] = "Cập nhật combo món ăn thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _comboService.DeleteAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Xóa combo món ăn thành công!"
            : result.ErrorMessage ?? "Không thể xóa combo";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetVariants(int productId)
    {
        var products = await _comboService.GetProductOptionsAsync();
        var product = products.FirstOrDefault(p => p.Id == productId);
        return Json(product?.Variants ?? new List<ComboVariantOptionViewModel>());
    }
}
```

- [ ] **Step 2: Create `Areas/Admin/Views/Combo/Index.cshtml`**

```html
@model Fruitables.ViewModels.ComboListViewModel
@{
    ViewData["Title"] = "Combo món ăn";
    Layout = "~/Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml";
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <h4 class="mb-1">Combo món ăn</h4>
        <nav aria-label="breadcrumb">
            <ol class="breadcrumb mb-0">
                <li class="breadcrumb-item active">Combo món ăn</li>
            </ol>
        </nav>
    </div>
    <a asp-action="Create" class="btn btn-primary">
        <i class="fas fa-plus me-2"></i>Thêm combo
    </a>
</div>

<div class="admin-card">
    <div class="card-header">
        <i class="fas fa-utensils me-2"></i>Danh sách combo
    </div>
    <div class="card-body p-0">
        <div class="table-responsive">
            <table class="table table-hover admin-table mb-0">
                <thead>
                    <tr>
                        <th style="width:80px">Ảnh</th>
                        <th>Tên combo</th>
                        <th>Số món</th>
                        <th>Tổng giá hiện tại</th>
                        <th>Trạng thái</th>
                        <th style="width:140px">Thao tác</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var item in Model.Items)
                    {
                        <tr>
                            <td>
                                @if (!string.IsNullOrEmpty(item.ImageUrl))
                                {
                                    <img src="@item.ImageUrl" alt="@item.Name" class="img-thumbnail" style="width:60px;height:60px;object-fit:cover" />
                                }
                                else
                                {
                                    <span class="text-muted">—</span>
                                }
                            </td>
                            <td>@item.Name</td>
                            <td>@item.ItemCount món</td>
                            <td>@item.TotalPrice.ToString("N0") đ</td>
                            <td>
                                @if (item.IsActive)
                                {
                                    <span class="badge bg-success">Kích hoạt</span>
                                }
                                else
                                {
                                    <span class="badge bg-secondary">Tắt</span>
                                }
                            </td>
                            <td>
                                <a asp-action="Edit" asp-route-id="@item.Id" class="btn btn-sm btn-outline-primary">
                                    <i class="fas fa-edit"></i>
                                </a>
                                <form asp-action="Delete" asp-route-id="@item.Id" method="post" class="d-inline" onsubmit="return confirm('Xóa combo này?');">
                                    @Html.AntiForgeryToken()
                                    <button type="submit" class="btn btn-sm btn-outline-danger">
                                        <i class="fas fa-trash"></i>
                                    </button>
                                </form>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
</div>
```

- [ ] **Step 3: Create `Areas/Admin/Views/Combo/_ComboItemRow.cshtml`**

```html
@model Fruitables.ViewModels.ComboItemFormModel
@{
    var index = (int)(ViewData["Index"] ?? 0);
    var products = (List<Fruitables.ViewModels.ComboProductOptionViewModel>)ViewData["Products"]!;
}

<div class="row combo-item-row align-items-end mb-2">
    <div class="col-md-5">
        <label class="form-label">Sản phẩm</label>
        <select name="Items[@index].ProductId" class="form-select product-select" required>
            <option value="">-- chọn --</option>
            @foreach (var p in products)
            {
                <option value="@p.Id" selected="@(p.Id == Model.ProductId)">@p.Name</option>
            }
        </select>
    </div>
    <div class="col-md-3">
        <label class="form-label">Biến thể</label>
        <select name="Items[@index].ProductVariantId" class="form-select variant-select">
            <option value="">-- mặc định --</option>
            @{
                var selectedProduct = products.FirstOrDefault(p => p.Id == Model.ProductId);
            }
            @if (selectedProduct != null)
            {
                @foreach (var v in selectedProduct.Variants)
                {
                    <option value="@v.Id" selected="@(v.Id == Model.ProductVariantId)">@v.Name</option>
                }
            }
        </select>
    </div>
    <div class="col-md-2">
        <label class="form-label">SL</label>
        <input name="Items[@index].Quantity" class="form-control" type="number" min="1" value="@Model.Quantity" required />
    </div>
    <div class="col-md-2">
        <button type="button" class="btn btn-outline-danger remove-item w-100">
            <i class="fas fa-trash"></i>
        </button>
    </div>
    <input type="hidden" name="Items[@index].SortOrder" value="@index" />
</div>
```

- [ ] **Step 4: Create `Areas/Admin/Views/Combo/_ComboForm.cshtml`**

```html
@model Fruitables.ViewModels.ComboFormViewModel

<form asp-action="@(Model.Id == 0 ? "Create" : "Edit")" method="post">
    <input type="hidden" asp-for="Id" />
    <div asp-validation-summary="ModelOnly" class="alert alert-danger" role="alert"></div>

    <div class="row">
        <div class="col-md-6 mb-3">
            <label asp-for="Name" class="form-label">Tên combo <span class="text-danger">*</span></label>
            <input asp-for="Name" class="form-control" placeholder="Ví dụ: Canh chua cá" />
            <span asp-validation-for="Name" class="text-danger"></span>
        </div>
        <div class="col-md-6 mb-3">
            <label asp-for="Slug" class="form-label">Slug</label>
            <input asp-for="Slug" class="form-control" placeholder="Tự động tạo nếu để trống" />
            <span asp-validation-for="Slug" class="text-danger"></span>
        </div>
    </div>

    <div class="row">
        <div class="col-md-6 mb-3">
            <label asp-for="ImageUrl" class="form-label">Hình ảnh (URL)</label>
            <input asp-for="ImageUrl" class="form-control" placeholder="URL hình ảnh combo" />
        </div>
        <div class="col-md-3 mb-3">
            <label asp-for="SortOrder" class="form-label">Thứ tự hiển thị</label>
            <input asp-for="SortOrder" class="form-control" type="number" min="0" />
        </div>
        <div class="col-md-3 mb-3">
            <label class="form-label">Trạng thái</label>
            <div class="form-check form-switch mt-2">
                <input asp-for="IsActive" class="form-check-input" type="checkbox" />
                <label asp-for="IsActive" class="form-check-label">Kích hoạt combo</label>
            </div>
        </div>
    </div>

    <div class="mb-3">
        <label asp-for="Description" class="form-label">Mô tả</label>
        <textarea asp-for="Description" class="form-control" rows="2" placeholder="Mô tả ngắn về combo"></textarea>
    </div>

    <div class="admin-card mt-4">
        <div class="card-header d-flex justify-content-between align-items-center">
            <span><i class="fas fa-list me-2"></i>Nguyên liệu</span>
            <button type="button" id="addComboItem" class="btn btn-sm btn-primary">
                <i class="fas fa-plus me-1"></i>Thêm món
            </button>
        </div>
        <div class="card-body">
            <div id="comboItemsContainer">
                @for (int i = 0; i < Model.Items.Count; i++)
                {
                    var vd = new ViewDataDictionary(ViewData) { { "Index", i }, { "Products", Model.Products } };
                    <partial name="_ComboItemRow" model="Model.Items[i]" view-data="vd" />
                }
            </div>
        </div>
    </div>

    <hr class="my-3" />
    <div class="d-flex gap-2">
        <button type="submit" class="btn btn-primary">
            <i class="fas fa-save me-2"></i>Lưu combo
        </button>
        <a asp-action="Index" class="btn btn-outline-secondary">Hủy</a>
    </div>
</form>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    <script>
        const productOptions = @Html.Raw(Json.Serialize(Model.Products.Select(p => new { p.Id, p.Name, Variants = p.Variants.Select(v => new { v.Id, v.Name }) })));
        let itemIndex = @(Model.Items.Count);

        function buildOptions(items, selectedId) {
            return '<option value="">-- chọn --</option>' +
                items.map(i => '<option value="' + i.id + '"' + (i.id == selectedId ? ' selected' : '') + '>' + i.name + '</option>').join('');
        }

        function buildVariantSelect(productId, selectedVariantId) {
            const product = productOptions.find(p => p.id == productId);
            if (!product || !product.variants.length) return '<option value="">Không có biến thể</option>';
            return '<option value="">-- mặc định --</option>' +
                product.variants.map(v => '<option value="' + v.id + '"' + (v.id == selectedVariantId ? ' selected' : '') + '>' + v.name + '</option>').join('');
        }

        function refreshVariantSelect(productSelect) {
            const row = productSelect.closest('.combo-item-row');
            const variantSelect = row.querySelector('.variant-select');
            const selectedVariant = variantSelect.value;
            variantSelect.innerHTML = buildVariantSelect(productSelect.value, selectedVariant);
        }

        document.getElementById('addComboItem').addEventListener('click', function () {
            const container = document.getElementById('comboItemsContainer');
            const html = `
                <div class="row combo-item-row align-items-end mb-2">
                    <div class="col-md-5">
                        <label class="form-label">Sản phẩm</label>
                        <select name="Items[${itemIndex}].ProductId" class="form-select product-select" required>
                            ${buildOptions(productOptions, '')}
                        </select>
                    </div>
                    <div class="col-md-3">
                        <label class="form-label">Biến thể</label>
                        <select name="Items[${itemIndex}].ProductVariantId" class="form-select variant-select">
                            <option value="">-- mặc định --</option>
                        </select>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label">SL</label>
                        <input name="Items[${itemIndex}].Quantity" class="form-control" type="number" min="1" value="1" required />
                    </div>
                    <div class="col-md-2">
                        <button type="button" class="btn btn-outline-danger remove-item w-100">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                    <input type="hidden" name="Items[${itemIndex}].SortOrder" value="${itemIndex}" />
                </div>`;
            container.insertAdjacentHTML('beforeend', html);
            itemIndex++;
        });

        document.getElementById('comboItemsContainer').addEventListener('change', function (e) {
            if (e.target.classList.contains('product-select')) {
                refreshVariantSelect(e.target);
            }
        });

        document.getElementById('comboItemsContainer').addEventListener('click', function (e) {
            if (e.target.closest('.remove-item')) {
                e.target.closest('.combo-item-row').remove();
            }
        });
    </script>
}
```

- [ ] **Step 5: Create `Areas/Admin/Views/Combo/Create.cshtml`**

```html
@model Fruitables.ViewModels.CreateComboViewModel
@{
    ViewData["Title"] = "Thêm combo món ăn";
    Layout = "~/Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml";
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <h4 class="mb-1">Thêm combo món ăn</h4>
        <nav aria-label="breadcrumb">
            <ol class="breadcrumb mb-0">
                <li class="breadcrumb-item"><a asp-action="Index">Combo món ăn</a></li>
                <li class="breadcrumb-item active">Thêm mới</li>
            </ol>
        </nav>
    </div>
    <a asp-action="Index" class="btn btn-outline-secondary">
        <i class="fas fa-arrow-left me-2"></i>Quay lại
    </a>
</div>

<partial name="_ComboForm" model="Model" />
```

- [ ] **Step 6: Create `Areas/Admin/Views/Combo/Edit.cshtml`**

```html
@model Fruitables.ViewModels.EditComboViewModel
@{
    ViewData["Title"] = "Sửa combo món ăn";
    Layout = "~/Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml";
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <h4 class="mb-1">Sửa combo món ăn</h4>
        <nav aria-label="breadcrumb">
            <ol class="breadcrumb mb-0">
                <li class="breadcrumb-item"><a asp-action="Index">Combo món ăn</a></li>
                <li class="breadcrumb-item active">Sửa</li>
            </ol>
        </nav>
    </div>
    <a asp-action="Index" class="btn btn-outline-secondary">
        <i class="fas fa-arrow-left me-2"></i>Quay lại
    </a>
</div>

<partial name="_ComboForm" model="Model" />
```

- [ ] **Step 7: Modify `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`**

Add this list item inside the "Quản lý cửa hàng" `<ul>` after the "Sản phẩm" link:

```html
        <li class="sidebar-nav-item">
            <a asp-action="Index" asp-controller="Combo" asp-area="Admin"
               class="sidebar-nav-link @(controller == "Combo" ? "active" : "")">
                <i class="fas fa-utensils"></i>
                <span>Combo món ăn</span>
            </a>
        </li>
```

- [ ] **Step 8: Build and commit**

Run:

```bash
dotnet build Fruitables.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

Commit:

```bash
git add Areas/Admin/Controllers/ComboController.cs Areas/Admin/Views/Combo/ Areas/Admin/Views/Shared/_AdminSidebar.cshtml
git commit -m "feat(combo): add admin CRUD UI"
```


### Task 5: Build storefront "add combo to cart" UI

**Files:**
- Create: `Controllers/ComboController.cs`
- Create: `Views/Shop/_ComboSection.cshtml`
- Modify: `Views/Shop/Index.cshtml`

**Interfaces:**
- Consumes: `IComboService` from Task 3 and existing `ICartService`.
- Produces: `POST /Combo/AddToCart` route; combo cards rendered on `/Shop`.

- [ ] **Step 1: Create `Controllers/ComboController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;

namespace Fruitables.Controllers;

public class ComboController : Controller
{
    private readonly IComboService _comboService;
    private readonly ICartService _cartService;

    public ComboController(IComboService comboService, ICartService cartService)
    {
        _comboService = comboService;
        _cartService = cartService;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddToCart(int id)
    {
        var sessionId = GetSessionId();
        var result = await _comboService.AddComboToCartAsync(sessionId, id, _cartService);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction("Index", "Cart");
    }

    private string GetSessionId()
    {
        var sessionId = HttpContext.Session.GetString("SessionId");
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
            HttpContext.Session.SetString("SessionId", sessionId);
        }
        return sessionId;
    }
}
```

- [ ] **Step 2: Create `Views/Shop/_ComboSection.cshtml`**

```html
@model IReadOnlyList<Fruitables.ViewModels.ComboCardViewModel>

@if (Model.Any())
{
    <section class="combo-section py-5">
        <div class="container">
            <div class="mb-4">
                <span class="shop-label">Gợi ý hôm nay</span>
                <h2 class="mb-0">Mua theo món</h2>
            </div>
            <div class="row g-4">
                @foreach (var combo in Model)
                {
                    <div class="col-md-6 col-lg-4">
                        <div class="card h-100">
                            @if (!string.IsNullOrEmpty(combo.ImageUrl))
                            {
                                <img src="@combo.ImageUrl" class="card-img-top" alt="@combo.Name" style="height:180px;object-fit:cover" />
                            }
                            <div class="card-body d-flex flex-column">
                                <h5 class="card-title">@combo.Name</h5>
                                @if (!string.IsNullOrEmpty(combo.Description))
                                {
                                    <p class="card-text text-muted">@combo.Description</p>
                                }
                                <ul class="list-unstyled flex-grow-1">
                                    @foreach (var item in combo.Items)
                                    {
                                        <li class="d-flex justify-content-between align-items-center mb-1">
                                            <span>
                                                @item.ProductName
                                                @if (!string.IsNullOrEmpty(item.VariantName))
                                                {
                                                    <small class="text-muted">(@item.VariantName)</small>
                                                }
                                                <small class="text-muted">× @item.Quantity</small>
                                            </span>
                                            @if (item.IsAvailable)
                                            {
                                                <span>@((item.UnitPrice * item.Quantity).ToString("N0")) đ</span>
                                            }
                                            else
                                            {
                                                <span class="badge bg-secondary">@item.UnavailableReason</span>
                                            }
                                        </li>
                                    }
                                </ul>
                                <div class="d-flex justify-content-between align-items-center mt-3 pt-3 border-top">
                                    <strong class="text-primary">@combo.TotalPrice.ToString("N0") đ</strong>
                                    <form asp-controller="Combo" asp-action="AddToCart" method="post">
                                        @Html.AntiForgeryToken()
                                        <input type="hidden" name="id" value="@combo.Id" />
                                        <button type="submit" class="btn btn-primary">
                                            <i class="fas fa-cart-plus me-1"></i>Thêm cả combo
                                        </button>
                                    </form>
                                </div>
                            </div>
                        </div>
                    </div>
                }
            </div>
        </div>
    </section>
}
```

- [ ] **Step 3: Modify `Views/Shop/Index.cshtml`**

Add these two lines after the existing `@using` directives at the top of the file:

```html
@using Fruitables.Services.Interfaces
@inject IComboService ComboService
```

Then add the following block immediately after the closing `</section>` of `shop-hero` and before the `<section class="shop-shell">`:

```html
@{
    var comboCards = await ComboService.GetActiveComboCardsAsync();
}
<partial name="_ComboSection" model="comboCards" />
```

The final structure around the insertion point should look like:

```html
    </section>

    @{
        var comboCards = await ComboService.GetActiveComboCardsAsync();
    }
    <partial name="_ComboSection" model="comboCards" />

    <section class="shop-shell">
```

- [ ] **Step 4: Build and commit**

Run:

```bash
dotnet build Fruitables.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

Commit:

```bash
git add Controllers/ComboController.cs Views/Shop/_ComboSection.cshtml Views/Shop/Index.cshtml
git commit -m "feat(combo): add storefront combo section and add-to-cart"
```

---

### Task 6: Verify end-to-end and commit final state

**Files:** all changed files

**Interfaces:** end-to-end manual verification

- [ ] **Step 1: Full build**

Run:

```bash
dotnet build Fruitables.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 2: Run the application**

Run:

```bash
dotnet run --project Fruitables.csproj
```

Open the site (usually `https://localhost:7001` or `http://localhost:5001` depending on `launchSettings.json`).

- [ ] **Step 3: Manual verification checklist**

Use the browser or Playwright MCP to verify:

1. **Admin sidebar** shows "Combo món ăn" under "Quản lý cửa hàng".
2. **Admin list** (`/Admin/Combo`) loads without errors and shows "Thêm combo" button.
3. **Create combo** (`/Admin/Combo/Create`):
   - Fill name, slug, image URL, description.
   - Add 2-3 ingredient rows: pick products, optionally pick variants, set quantities.
   - Save → redirect to list with success message.
4. **Edit combo** loads the saved rows correctly and updates successfully.
5. **Delete combo** removes the combo and returns to list.
6. **Shop page** (`/Shop`) displays the combo section with cards.
   - Each card shows correct product names, variant names, quantities, and total price.
   - Price on card equals sum of current effective prices (test by setting a `PriceSchedule` discount and reloading).
7. **Add combo to cart** (logged-in user):
   - Click "Thêm cả combo" → redirect to `/Cart`.
   - Cart contains the correct products/variants/quantities.
   - TempData message confirms added count.
8. **Unavailable item handling**:
   - Set one ingredient's stock to 0 or deactivate it.
   - Reload Shop → item shows "tạm hết" badge and is excluded from total.
   - Click "Thêm cả combo" → cart receives remaining items; message lists skipped item.

- [ ] **Step 4: Final commit**

If any small fixes were needed during verification, commit them separately with clear messages. Once all checks pass:

```bash
git status
git add -A  # only if there are uncommitted verification fixes
git commit -m "feat(combo): verify meal combo feature end-to-end"
```

---

## Self-Review

**Spec coverage:**
- Data model (`Combo`/`ComboItem`) → Task 1.
- EF config (unique slug, cascade/restrict delete) → Task 1.
- Admin CRUD → Task 4.
- Storefront section on Shop → Task 5.
- Add combo to cart reusing `ICartService` → Task 3/5.
- Real-time pricing via `IProductPricingService` → Task 3.
- Skip unavailable items with messages → Task 3.
- No unit tests; manual verification → Task 6.

**Placeholder scan:** All steps contain concrete file paths, full code blocks, exact commands, and expected outputs. No "TBD", "TODO", or vague instructions.

**Type consistency:** `IComboService` method signatures, view model names, and controller usages match across tasks. `ComboFormViewModel` is used for both Create and Edit; `CreateComboViewModel`/`EditComboViewModel` inherit from it and are returned by controller actions.
