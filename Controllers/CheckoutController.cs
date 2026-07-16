using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Fruitables.Repositories.Interfaces;
using Fruitables.Models;

namespace Fruitables.Controllers;

// Controller checkout (thanh toán): xác nhận giỏ hàng, chọn địa chỉ giao hàng, đặt hàng.
// Yêu cầu đăng nhập ([Authorize]).
[Authorize]
public class CheckoutController : Controller
{
    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly IAddressService _addressService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVietnamAddressService _vietnamAddressService;
    private readonly IShippingService _shippingService;
    private readonly ILogger<CheckoutController> _logger;
    
    // Keys lưu snapshot phí ship trong session (tránh thay đổi giữa Index → PlaceOrder)
    private const string ShippingFeeSnapshotKey = "ShippingFeeSnapshot";
    private const string ShippingZoneSnapshotKey = "ShippingZoneSnapshot";
    private const string ShippingSnapshotTimeKey = "ShippingSnapshotTime";
    private const string ShippingDistrictSnapshotKey = "ShippingDistrictSnapshot";

    // Inject 7 dependencies: cart, order, address, UoW, VN address, shipping, logger
    public CheckoutController(
        ICartService cartService, 
        IOrderService orderService, 
        IAddressService addressService,
        IUnitOfWork unitOfWork,
        IVietnamAddressService vietnamAddressService,
        IShippingService shippingService,
        ILogger<CheckoutController> logger)
    {
        _cartService = cartService;
        _orderService = orderService;
        _addressService = addressService;
        _unitOfWork = unitOfWork;
        _vietnamAddressService = vietnamAddressService;
        _shippingService = shippingService;
        _logger = logger;
    }

    // GET: Hiển thị trang checkout — load giỏ hàng + địa chỉ đã lưu + snapshot phí ship
    public async Task<IActionResult> Index()
    {
        var sessionId = GetSessionId();
        var userId = GetCurrentUserId();
        
        List<AddressViewModel> addressViewModels = new();
        int? defaultAddressId = null;
        string? defaultCommune = null;
        int? defaultGhnDistrictId = null;
        string? defaultGhnWardCode = null;
        
        if (userId.HasValue)
        {
            var addresses = await _addressService.GetUserAddressesAsync(userId.Value);
            addressViewModels = addresses.Select(a => new AddressViewModel
            {
                Id = a.Id,
                FullName = a.FullName,
                Phone = a.Phone,
                ProvinceCode = a.ProvinceCode,
                ProvinceName = a.ProvinceName,
                CommuneCode = a.CommuneCode,
                CommuneName = a.CommuneName,
                GhnDistrictId = a.GhnDistrictId,
                GhnWardCode = a.GhnWardCode,
                StreetAddress = a.StreetAddress,
                IsDefault = a.IsDefault
            }).ToList();

            var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault);
            if (defaultAddress != null)
            {
                defaultAddressId = defaultAddress.Id;
                defaultCommune = defaultAddress.CommuneName;
                defaultGhnDistrictId = defaultAddress.GhnDistrictId;
                defaultGhnWardCode = defaultAddress.GhnWardCode;
            }
        }
        
        // Load cart, tính phí ship theo commune (xã) mặc định
        var cart = await _cartService.RepriceForCheckoutAsync(sessionId)
            ?? await _cartService.GetCartAsync(sessionId, defaultCommune);
        if (defaultGhnDistrictId.HasValue && !string.IsNullOrWhiteSpace(defaultGhnWardCode))
        {
            var shippingInfo = await _shippingService.CalculateShippingAsync(
                cart.Subtotal,
                defaultCommune ?? string.Empty,
                defaultGhnDistrictId,
                defaultGhnWardCode,
                cart.ShippingPackage);

            ApplyShipping(cart, shippingInfo);
        }

        // Giỏ hàng rỗng → redirect về cart
        if (!cart.Items.Any())
        {
            return RedirectToAction("Index", "Cart");
        }
        if (cart.Items.Any(item => !item.IsAvailable || item.StockQuantity < item.Quantity))
        {
            TempData["Error"] = "Một số sản phẩm hoặc biến thể không còn bán hoặc không đủ tồn kho. Vui lòng kiểm tra lại giỏ hàng.";
            return RedirectToAction("Index", "Cart");
        }
        
        // Lưu snapshot phí ship khi vào checkout (chống thay đổi phí giữa chừng)
        if (cart.ShippingInfo != null)
        {
            SaveShippingSnapshot(cart.ShippingInfo, defaultCommune);
        }

        ViewBag.CartCount = cart.Items.Sum(i => i.Quantity);
        ViewBag.Cart = cart;
        ViewBag.SavedAddresses = addressViewModels;
        
        var model = new CheckoutViewModel
        {
            SelectedAddressId = defaultAddressId,
            PricingToken = cart.PricingToken
        };
        
        return View(model);
    }

    // POST: Mua ngay — thêm sản phẩm vào giỏ rồi redirect thẳng tới checkout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyNow(int productId, int quantity = 1, int? variantId = null)
    {
        var sessionId = GetSessionId();
        
        await _cartService.AddToCartAsync(sessionId, productId, quantity, variantId);
        
        return RedirectToAction(nameof(Index));
    }

    // POST: Xử lý đặt hàng
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
    {
        _logger.LogInformation("PlaceOrder called - SelectedAddressId: {AddressId}, PaymentMethod: {Payment}", 
            model.SelectedAddressId, model.PaymentMethod);
        
        var sessionId = GetSessionId();
        var userId = GetCurrentUserId();
        
        // Nếu chọn địa chỉ đã lưu → không validate các field address (lấy từ DB)
        if (model.SelectedAddressId.HasValue)
        {
            ModelState.Remove(nameof(model.FirstName));
            ModelState.Remove(nameof(model.ProvinceCode));
            ModelState.Remove(nameof(model.CommuneCode));
            ModelState.Remove(nameof(model.StreetAddress));
            ModelState.Remove(nameof(model.Mobile));
        }
        
        // Lấy commune từ địa chỉ đã chọn hoặc từ form
        string? district = null;
        int? ghnDistrictId = null;
        string? ghnWardCode = null;
        if (model.SelectedAddressId.HasValue && userId.HasValue)
        {
            var selectedAddress = await _unitOfWork.Addresses.Query()
                .FirstOrDefaultAsync(a => a.Id == model.SelectedAddressId.Value && a.UserId == userId.Value);
            if (selectedAddress == null)
            {
                ModelState.AddModelError(nameof(model.SelectedAddressId), "Dia chi giao hang khong hop le.");
            }

            district = selectedAddress?.CommuneName;
            ghnDistrictId = selectedAddress?.GhnDistrictId;
            ghnWardCode = selectedAddress?.GhnWardCode;
        }
        
        var submittedPricingToken = model.PricingToken;
        var cart = await _cartService.RepriceForCheckoutAsync(sessionId)
            ?? await _cartService.GetCartAsync(sessionId, district);
        if (cart.Items.Any(item => !item.IsAvailable || item.StockQuantity < item.Quantity))
            ModelState.AddModelError(string.Empty, "Một số sản phẩm hoặc biến thể không còn bán hoặc không đủ tồn kho.");
        if (!string.IsNullOrEmpty(cart.PricingToken) && !string.Equals(submittedPricingToken, cart.PricingToken, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Giá hoặc mã giảm giá vừa thay đổi. Vui lòng kiểm tra tổng tiền và xác nhận đặt hàng lại.");
            model.PricingToken = cart.PricingToken;
        }

        // Validation thất bại → reload lại checkout với lỗi
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => $"{x.Key}: {string.Join(", ", x.Value!.Errors.Select(e => e.ErrorMessage))}")
                .ToList();
            _logger.LogWarning("PlaceOrder validation failed: {Errors}", string.Join(" | ", errors));
            
            // Reload địa chỉ cho display
            if (userId.HasValue)
            {
                var addresses = await _addressService.GetUserAddressesAsync(userId.Value);
                ViewBag.SavedAddresses = addresses.Select(a => new AddressViewModel
                {
                    Id = a.Id,
                    FullName = a.FullName,
                    Phone = a.Phone,
                    ProvinceCode = a.ProvinceCode,
                    ProvinceName = a.ProvinceName,
                    CommuneCode = a.CommuneCode,
                    CommuneName = a.CommuneName,
                    GhnDistrictId = a.GhnDistrictId,
                    GhnWardCode = a.GhnWardCode,
                    StreetAddress = a.StreetAddress,
                    IsDefault = a.IsDefault
                }).ToList();
            }
            else
            {
                ViewBag.SavedAddresses = new List<AddressViewModel>();
            }

            ViewBag.CartCount = cart.Items.Sum(i => i.Quantity);
            ViewBag.Cart = cart;
            return View("Index", model);
        }

        // Recalculate from the submitted address so an old snapshot cannot price a different address.
        var shippingInfo = await _shippingService.CalculateShippingAsync(
            cart.Subtotal,
            district ?? string.Empty,
            ghnDistrictId,
            ghnWardCode,
            cart.ShippingPackage);
        ApplyShipping(cart, shippingInfo);
        model.Cart = cart;

        try
        {
            var order = await _orderService.CreateOrderAsync(model, sessionId, userId);
            
            // Xóa snapshot sau khi đặt hàng thành công
            ClearShippingSnapshot();
            
            return RedirectToAction(nameof(Confirmation), new { orderNumber = order.OrderNumber });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("PlaceOrder failed: {Message}", ex.Message);
            ModelState.AddModelError(string.Empty, ex.Message);

            if (userId.HasValue)
            {
                var addresses = await _addressService.GetUserAddressesAsync(userId.Value);
                ViewBag.SavedAddresses = addresses.Select(a => new AddressViewModel
                {
                    Id = a.Id,
                    FullName = a.FullName,
                    Phone = a.Phone,
                    ProvinceCode = a.ProvinceCode,
                    ProvinceName = a.ProvinceName,
                    CommuneCode = a.CommuneCode,
                    CommuneName = a.CommuneName,
                    GhnDistrictId = a.GhnDistrictId,
                    GhnWardCode = a.GhnWardCode,
                    StreetAddress = a.StreetAddress,
                    IsDefault = a.IsDefault
                }).ToList();
            }
            else
            {
                ViewBag.SavedAddresses = new List<AddressViewModel>();
            }

            ViewBag.CartCount = cart.Items.Sum(i => i.Quantity);
            ViewBag.Cart = cart;
            return View("Index", model);
        }
    }

    // GET: Trang xác nhận đặt hàng thành công (hiện thông tin order)
    public async Task<IActionResult> Confirmation(string orderNumber)
    {
        var sessionId = GetSessionId();
        ViewBag.CartCount = await _cartService.GetCartCountAsync(sessionId);

        var order = await _orderService.GetOrderByNumberAsync(orderNumber);
        if (order == null) return NotFound();

        ViewBag.Order = order;
        return View();
    }

    // Helper: lấy/tạo SessionId cho giỏ hàng
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
    
    // Helper: lấy userId từ claims cookie
    private int? GetCurrentUserId()
    {
        if (User.Identity?.IsAuthenticated != true)
            return null;
            
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
            return userId;
            
        return null;
    }
    
    // Lưu snapshot phí ship vào session (tại thời điểm vào checkout) — chống thay đổi giữa chừng
    private void SaveShippingSnapshot(ShippingInfo shippingInfo, string? district)
    {
        HttpContext.Session.SetString(ShippingFeeSnapshotKey, shippingInfo.ShippingFee.ToString());
        HttpContext.Session.SetString(ShippingZoneSnapshotKey, ((int)shippingInfo.Zone).ToString());
        HttpContext.Session.SetString(ShippingSnapshotTimeKey, DateTime.UtcNow.ToString("O"));
        HttpContext.Session.SetString(ShippingDistrictSnapshotKey, district ?? string.Empty);
    }
    
    // Đọc phí ship từ snapshot trong session
    private decimal? GetShippingFeeFromSnapshot()
    {
        var feeStr = HttpContext.Session.GetString(ShippingFeeSnapshotKey);
        if (decimal.TryParse(feeStr, out var fee))
            return fee;
        return null;
    }
    
    // Đọc zone từ snapshot trong session
    private ShippingZone? GetShippingZoneFromSnapshot()
    {
        var zoneStr = HttpContext.Session.GetString(ShippingZoneSnapshotKey);
        if (int.TryParse(zoneStr, out var zone) && Enum.IsDefined(typeof(ShippingZone), zone))
            return (ShippingZone)zone;
        return null;
    }
    
    private static void ApplyShipping(CartViewModel cart, ShippingInfo shippingInfo)
    {
        cart.ShippingInfo = shippingInfo;
        cart.ShippingFee = shippingInfo.ShippingFee;
        cart.Total = cart.Subtotal + cart.ShippingFee - cart.Discount;
    }

    // Xóa snapshot khỏi session sau khi đặt hàng
    private void ClearShippingSnapshot()
    {
        HttpContext.Session.Remove(ShippingFeeSnapshotKey);
        HttpContext.Session.Remove(ShippingZoneSnapshotKey);
        HttpContext.Session.Remove(ShippingSnapshotTimeKey);
        HttpContext.Session.Remove(ShippingDistrictSnapshotKey);
    }
}
