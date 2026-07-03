# GHN Package Sizing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Calculate GHN shipping package weight and dimensions from actual cart kilograms instead of using one fixed configured package size.

**Architecture:** Add one small `ShippingPackage` value object beside existing shipping models, derive it from cart item quantities, and pass it through existing shipping service calls. Keep GHN as the only shipping fee source; do not reintroduce manual zone fallback.

**Tech Stack:** ASP.NET Core MVC, EF Core, xUnit, Moq, existing `IShippingService`, `IGhnService`, `CartViewModel`, and checkout/cart controllers.

---

## File Structure

- Modify `Models/Shipping.cs`: add `ShippingPackage` with the approved kilogram-to-box rules.
- Modify `ViewModels/CartViewModel.cs`: expose the derived package from cart quantities.
- Modify `Services/Interfaces/IShippingService.cs`: add optional `ShippingPackage? package` to `CalculateShippingAsync`.
- Modify `Services/ShippingService.cs`: pass package weight/dimensions to GHN and fail without package instead of using defaults.
- Modify `Services/CartService.cs`: keep existing cart loading; no product weight usage.
- Modify `Controllers/CheckoutController.cs`: pass `cart.ShippingPackage` when calculating GHN shipping.
- Modify `Controllers/CartController.cs`: calculate package server-side from the session cart before AJAX shipping calculation.
- Modify tests:
  - `Tests/ShippingPackageTests.cs`
  - `Tests/ShippingServiceGhnTests.cs`
  - `Tests/CartControllerShippingTests.cs`

---

### Task 1: Add ShippingPackage Rules

**Files:**
- Modify: `Models/Shipping.cs`
- Create: `Tests/ShippingPackageTests.cs`

- [ ] **Step 1: Write failing package tier tests**

Create `Tests/ShippingPackageTests.cs`:

```csharp
using Fruitables.Models;
using Xunit;

namespace Fruitables.Tests;

public class ShippingPackageTests
{
    [Theory]
    [InlineData(1, 1000, 20, 15, 10)]
    [InlineData(2, 2000, 20, 15, 10)]
    [InlineData(3, 3000, 30, 20, 15)]
    [InlineData(5, 5000, 30, 20, 15)]
    [InlineData(6, 6000, 40, 30, 20)]
    public void FromTotalKg_UsesFruitBoxTiers(
        int totalKg,
        int expectedWeight,
        int expectedLength,
        int expectedWidth,
        int expectedHeight)
    {
        var package = ShippingPackage.FromTotalKg(totalKg);

        Assert.Equal(expectedWeight, package.Weight);
        Assert.Equal(expectedLength, package.Length);
        Assert.Equal(expectedWidth, package.Width);
        Assert.Equal(expectedHeight, package.Height);
    }

    [Fact]
    public void FromTotalKg_ClampsZeroAndNegativeWeightToZero()
    {
        var package = ShippingPackage.FromTotalKg(0);

        Assert.Equal(0, package.Weight);
        Assert.Equal(20, package.Length);
        Assert.Equal(15, package.Width);
        Assert.Equal(10, package.Height);
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~ShippingPackageTests" --no-restore
```

Expected: compile fails because `ShippingPackage` does not exist.

- [ ] **Step 3: Add the minimal ShippingPackage implementation**

Append this class to `Models/Shipping.cs`, inside the `Fruitables.Models` namespace and after `ShippingInfo`:

```csharp
    public sealed record ShippingPackage(int Weight, int Length, int Width, int Height)
    {
        public static ShippingPackage FromTotalKg(int totalKg)
        {
            var kg = Math.Max(0, totalKg);
            var weight = kg * 1000;

            return kg switch
            {
                <= 2 => new ShippingPackage(weight, 20, 15, 10),
                <= 5 => new ShippingPackage(weight, 30, 20, 15),
                _ => new ShippingPackage(weight, 40, 30, 20)
            };
        }
    }
```

- [ ] **Step 4: Verify package tier tests pass**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~ShippingPackageTests" --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Models/Shipping.cs Tests/ShippingPackageTests.cs
git commit -m "feat: derive GHN package size from kilograms"
```

---

### Task 2: Expose Package Data From Cart

**Files:**
- Modify: `ViewModels/CartViewModel.cs`

- [ ] **Step 1: Add a failing assertion to package tests for cart derivation**

Add this test to `Tests/ShippingPackageTests.cs`:

```csharp
[Fact]
public void CartViewModel_ShippingPackage_UsesSumOfItemQuantitiesAsKilograms()
{
    var cart = new Fruitables.ViewModels.CartViewModel
    {
        Items =
        {
            new Fruitables.ViewModels.CartItemViewModel { Quantity = 2 },
            new Fruitables.ViewModels.CartItemViewModel { Quantity = 3 }
        }
    };

    var package = cart.ShippingPackage;

    Assert.Equal(5000, package.Weight);
    Assert.Equal(30, package.Length);
    Assert.Equal(20, package.Width);
    Assert.Equal(15, package.Height);
}
```

- [ ] **Step 2: Run the failing test**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~ShippingPackageTests.CartViewModel_ShippingPackage_UsesSumOfItemQuantitiesAsKilograms" --no-restore
```

Expected: compile fails because `CartViewModel.ShippingPackage` does not exist.

- [ ] **Step 3: Add computed package property**

Modify `ViewModels/CartViewModel.cs`:

```csharp
public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }
    public decimal Discount { get; set; }
    public string? CouponMessage { get; set; }
    
    public ShippingInfo? ShippingInfo { get; set; }

    public ShippingPackage ShippingPackage =>
        ShippingPackage.FromTotalKg(Items.Sum(i => i.Quantity));
}
```

- [ ] **Step 4: Verify cart package test passes**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~ShippingPackageTests" --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add ViewModels/CartViewModel.cs Tests/ShippingPackageTests.cs
git commit -m "feat: expose cart shipping package"
```

---

### Task 3: Pass Derived Package Into GHN ShippingService

**Files:**
- Modify: `Services/Interfaces/IShippingService.cs`
- Modify: `Services/ShippingService.cs`
- Modify: `Tests/ShippingServiceGhnTests.cs`

- [ ] **Step 1: Update existing GHN service test to expect derived package values**

In `Tests/ShippingServiceGhnTests.cs`, update `CalculateShippingAsync_UsesGhnFee_WhenAddressCodesExist` setup and call:

```csharp
ghn.Setup(service => service.CalculateFeeAsync(
        1442,
        "20101",
        3000,
        30,
        20,
        15,
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(32000m);

var result = await service.CalculateShippingAsync(
    100000m,
    "Phuong Ben Nghe",
    1442,
    "20101",
    ShippingPackage.FromTotalKg(3));
```

- [ ] **Step 2: Add failing test for missing package**

Add this test to `Tests/ShippingServiceGhnTests.cs`:

```csharp
[Fact]
public async Task CalculateShippingAsync_DoesNotCallGhn_WhenPackageMissing()
{
    var settings = CreateSettingsService();
    var ghn = new Mock<IGhnService>();
    var service = new ShippingService(
        settings.Object,
        NullLogger<ShippingService>.Instance,
        ghn.Object,
        CreateOptions());

    var result = await service.CalculateShippingAsync(100000m, "Phuong Ben Nghe", 1442, "20101");

    Assert.Equal(0m, result.ShippingFee);
    Assert.Equal("Khong tinh duoc phi van chuyen GHN", result.Message);
    ghn.Verify(service => service.CalculateFeeAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()),
        Times.Never);
}
```

- [ ] **Step 3: Run failing shipping service tests**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~ShippingServiceGhnTests" --no-restore
```

Expected: compile fails because `CalculateShippingAsync` does not accept `ShippingPackage`.

- [ ] **Step 4: Update IShippingService signature**

Modify `Services/Interfaces/IShippingService.cs`:

```csharp
Task<ShippingInfo> CalculateShippingAsync(
    decimal subtotal,
    string district,
    int? ghnDistrictId = null,
    string? ghnWardCode = null,
    ShippingPackage? package = null);
```

- [ ] **Step 5: Update ShippingService implementation**

Modify `Services/ShippingService.cs` method signature and GHN call:

```csharp
public async Task<ShippingInfo> CalculateShippingAsync(
    decimal subtotal,
    string district,
    int? ghnDistrictId = null,
    string? ghnWardCode = null,
    ShippingPackage? package = null)
{
    if (subtotal > 0
        && package != null
        && package.Weight > 0
        && ghnDistrictId.HasValue
        && !string.IsNullOrWhiteSpace(ghnWardCode))
    {
        var ghnFee = await _ghnService.CalculateFeeAsync(
            ghnDistrictId.Value,
            ghnWardCode,
            package.Weight,
            package.Length,
            package.Width,
            package.Height);

        if (ghnFee.HasValue)
        {
            return new ShippingInfo
            {
                ShippingFee = ghnFee.Value,
                Zone = ShippingZone.Zone3_Remote,
                Message = "Phi van chuyen GHN"
            };
        }
    }

    return new ShippingInfo
    {
        ShippingFee = 0m,
        Zone = ShippingZone.Zone3_Remote,
        Message = subtotal > 0
            ? "Khong tinh duoc phi van chuyen GHN"
            : string.Empty
    };
}
```

Do not use `_ghnOptions.DefaultWeight`, `_ghnOptions.DefaultLength`, `_ghnOptions.DefaultWidth`, or `_ghnOptions.DefaultHeight` in this method.

- [ ] **Step 6: Verify shipping service tests pass**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~ShippingServiceGhnTests" --no-restore
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add Services/Interfaces/IShippingService.cs Services/ShippingService.cs Tests/ShippingServiceGhnTests.cs
git commit -m "feat: send derived package to GHN"
```

---

### Task 4: Pass Package From Checkout and Cart AJAX

**Files:**
- Modify: `Controllers/CheckoutController.cs`
- Modify: `Controllers/CartController.cs`
- Modify: `Tests/CartControllerShippingTests.cs`

- [ ] **Step 1: Update CartController test to expect server-derived package**

Modify `Tests/CartControllerShippingTests.cs` so the controller uses a session cart package. Use this complete test body:

```csharp
[Fact]
public async Task CalculateShippingAjax_PassesGhnCodesAndCartPackageToShippingService()
{
    var shippingService = new Mock<IShippingService>();
    shippingService
        .Setup(service => service.CalculateShippingAsync(
            417000m,
            "Phuong An Hai",
            1528,
            "910363",
            ShippingPackage.FromTotalKg(3)))
        .ReturnsAsync(new ShippingInfo
        {
            ShippingFee = 53900m,
            Zone = ShippingZone.Zone3_Remote,
            Message = "Phi van chuyen GHN"
        });

    var cartService = new Mock<ICartService>();
    cartService
        .Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
        .ReturnsAsync(new CartViewModel
        {
            Items =
            {
                new CartItemViewModel { Quantity = 3 }
            },
            Subtotal = 417000m
        });

    var controller = new CartController(
        cartService.Object,
        shippingService.Object,
        Mock.Of<ICouponService>());
    controller.ControllerContext.HttpContext = new DefaultHttpContext
    {
        Session = new TestSession()
    };

    await controller.CalculateShippingAjax(new CartController.CalculateShippingRequest
    {
        Subtotal = 417000m,
        District = "Phuong An Hai",
        GhnDistrictId = 1528,
        GhnWardCode = "910363"
    });

    shippingService.Verify(
        service => service.CalculateShippingAsync(
            417000m,
            "Phuong An Hai",
            1528,
            "910363",
            ShippingPackage.FromTotalKg(3)),
        Times.Once);
}
```

If `TestSession` does not exist in the test project, add this private class to `Tests/CartControllerShippingTests.cs`:

```csharp
private sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public IEnumerable<string> Keys => _store.Keys;
    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsAvailable => true;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
```

Also add these usings if missing:

```csharp
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
```

- [ ] **Step 2: Run failing CartController test**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~CartControllerShippingTests" --no-restore
```

Expected: FAIL because `CartController.CalculateShippingAjax` does not pass `ShippingPackage` yet.

- [ ] **Step 3: Update CheckoutController package calls**

In `Controllers/CheckoutController.cs`, update both shipping service calls.

For checkout GET:

```csharp
var shippingInfo = await _shippingService.CalculateShippingAsync(
    cart.Subtotal,
    defaultCommune ?? string.Empty,
    defaultGhnDistrictId,
    defaultGhnWardCode,
    cart.ShippingPackage);
```

For `PlaceOrder`:

```csharp
var shippingInfo = await _shippingService.CalculateShippingAsync(
    cart.Subtotal,
    district ?? string.Empty,
    ghnDistrictId,
    ghnWardCode,
    cart.ShippingPackage);
```

- [ ] **Step 4: Update CartController AJAX package call**

In `Controllers/CartController.cs`, replace the shipping calculation in `CalculateShippingAjax` with:

```csharp
var sessionId = GetSessionId();
var cart = await _cartService.GetCartAsync(sessionId, request.District ?? string.Empty);
var shippingInfo = await _shippingService.CalculateShippingAsync(
    cart.Subtotal,
    request.District ?? string.Empty,
    request.GhnDistrictId,
    request.GhnWardCode,
    cart.ShippingPackage);
```

Keep the existing JSON response shape.

- [ ] **Step 5: Verify controller tests pass**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~CartControllerShippingTests" --no-restore
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add Controllers/CheckoutController.cs Controllers/CartController.cs Tests/CartControllerShippingTests.cs
git commit -m "feat: use cart package for checkout shipping"
```

---

### Task 5: Final Verification and Cleanup

**Files:**
- Review: `Services/ShippingService.cs`
- Review: `Services/GhnOptions.cs`
- Review: `appsettings.json`

- [ ] **Step 1: Verify fixed package defaults are no longer used for GHN fee**

Run:

```powershell
rg -n "DefaultWeight|DefaultLength|DefaultWidth|DefaultHeight" Services Tests appsettings.json
```

Expected:

- `GhnOptions.cs` and `appsettings.json` may still contain these config fields.
- `Services/ShippingService.cs` must not use these fields in `CalculateShippingAsync`.

- [ ] **Step 2: Run GHN/package-focused tests**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --filter "FullyQualifiedName~ShippingPackageTests|FullyQualifiedName~ShippingServiceGhnTests|FullyQualifiedName~CartControllerShippingTests" --no-restore
```

Expected: PASS.

- [ ] **Step 3: Run full test suite**

Run:

```powershell
dotnet test .\Tests\Fruitables.Tests.csproj --no-restore
```

Expected: PASS. Existing unrelated warnings are acceptable; test failures are not.

- [ ] **Step 4: Optional manual GHN smoke test**

With a cart containing `3kg` and a saved address with GHN codes, open checkout and verify the GHN API request uses:

```text
weight = 3000
length = 30
width = 20
height = 15
```

If checking via code instead of browser, add temporary logging only locally and remove it before commit.

- [ ] **Step 5: Confirm working tree state**

Run:

```powershell
git status --short
```

Expected: only intentional implementation files from Tasks 1-4 are modified or committed. Do not stage `appsettings.json` if it contains local GHN secrets.
