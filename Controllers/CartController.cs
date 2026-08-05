using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Orders.Cart;
using Fruitables.Services.Pricing.Coupons;
using Fruitables.Services.Shipping.Delivery;

namespace Fruitables.Controllers;

// Controller giỏ hàng: thêm/sửa/xóa sản phẩm, tính phí ship, áp dụng mã giảm giá.
// Hỗ trợ cả form submit và AJAX endpoints. Dùng SessionId để định danh giỏ hàng (guest/anonymous).
public class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly IShippingService _shippingService;
    private readonly ICouponService _couponService;

    // Inject 3 service: cart (CRUD sản phẩm), shipping (tính phí vận chuyển), coupon (mã giảm giá)
    public CartController(ICartService cartService, IShippingService shippingService, ICouponService couponService)
    {
        _cartService = cartService;
        _shippingService = shippingService;
        _couponService = couponService;
    }

    // GET: Hiển thị giỏ hàng
    public async Task<IActionResult> Index()
    {
        var sessionId = GetSessionId();
        var cart = await _cartService.RepriceForCheckoutAsync(sessionId)
            ?? await _cartService.GetCartAsync(sessionId);
        ViewBag.CartCount = cart.Items.Sum(i => i.Quantity);
        return View(cart);
    }

    // POST: Thêm sản phẩm vào giỏ (yêu cầu đăng nhập)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddToCart(
        int productId,
        int quantity = 1,
        int? variantId = null)
    {
        var sessionId = GetSessionId();

        var result = await _cartService.AddToCartAsync(
            sessionId,
            productId,
            quantity,
            variantId);

        TempData[result.Success ? "Success" : "Error"] = result.Message;

        return RedirectToAction(nameof(Index));
    }

    // POST: Cập nhật số lượng sản phẩm (form submit)
    [HttpPost]
    public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
    {
        var sessionId = GetSessionId();
        await _cartService.UpdateQuantityAsync(sessionId, cartItemId, quantity);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateComboQuantity(int cartGroupId, int quantity)
    {
        var sessionId = GetSessionId();
        var result = await _cartService.UpdateComboQuantityAsync(sessionId, cartGroupId, quantity);
        if (!result.Success)
            TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCombo(int cartGroupId)
    {
        var sessionId = GetSessionId();
        await _cartService.RemoveComboAsync(sessionId, cartGroupId);
        return RedirectToAction(nameof(Index));
    }

    // POST: Xóa sản phẩm khỏi giỏ (form submit)
    [HttpPost]
    public async Task<IActionResult> RemoveFromCart(int cartItemId)
    {
        var sessionId = GetSessionId();
        await _cartService.RemoveFromCartAsync(sessionId, cartItemId);
        return RedirectToAction(nameof(Index));
    }

    // POST: Cập nhật số lượng bằng AJAX (JSON body) — trả về thông tin giỏ hàng mới
    [HttpPost]
    public async Task<IActionResult> UpdateQuantityAjax([FromBody] UpdateQuantityRequest request)
    {
        var sessionId = GetSessionId();
        await _cartService.UpdateQuantityAsync(sessionId, request.CartItemId, request.Quantity);
        var cart = await _cartService.GetCartAsync(sessionId);
        var item = cart.Items.FirstOrDefault(i => i.CartItemId == request.CartItemId);
        
        return Json(new
        {
            success = true,
            itemTotal = item?.Total ?? 0,
            actualQuantity = item?.Quantity ?? 0,
            stockQuantity = item?.StockQuantity ?? 0,
            subtotal = cart.Subtotal,
            shippingFee = cart.ShippingFee,
            discount = cart.Discount,
            total = cart.Total,
            cartCount = cart.Items.Sum(i => i.Quantity),
            itemRemoved = item == null,
            maxReached = item != null && item.Quantity >= item.StockQuantity,
            shippingInfo = cart.ShippingInfo != null ? new
            {
                shippingFee = cart.ShippingInfo.ShippingFee,
                zone = (int)cart.ShippingInfo.Zone,
                isFreeShipping = cart.ShippingInfo.IsFreeShipping,
                isReducedShipping = cart.ShippingInfo.IsReducedShipping,
                amountToFreeShipping = cart.ShippingInfo.AmountToFreeShipping,
                message = cart.ShippingInfo.Message
            } : null
        });
    }

    // POST: Xóa sản phẩm bằng AJAX — trả về thông tin giỏ hàng mới
    [HttpPost]
    public async Task<IActionResult> RemoveFromCartAjax([FromBody] RemoveFromCartRequest request)
    {
        var sessionId = GetSessionId();
        await _cartService.RemoveFromCartAsync(sessionId, request.CartItemId);
        var cart = await _cartService.GetCartAsync(sessionId);
        
        return Json(new
        {
            success = true,
            subtotal = cart.Subtotal,
            shippingFee = cart.ShippingFee,
            discount = cart.Discount,
            total = cart.Total,
            cartCount = cart.Items.Sum(i => i.Quantity),
            isEmpty = !cart.Items.Any(),
            shippingInfo = cart.ShippingInfo != null ? new
            {
                shippingFee = cart.ShippingInfo.ShippingFee,
                zone = (int)cart.ShippingInfo.Zone,
                isFreeShipping = cart.ShippingInfo.IsFreeShipping,
                isReducedShipping = cart.ShippingInfo.IsReducedShipping,
                amountToFreeShipping = cart.ShippingInfo.AmountToFreeShipping,
                message = cart.ShippingInfo.Message
            } : null
        });
    }
    
    // POST: Tính phí vận chuyển bằng AJAX — dùng ShippingService tính theo mã GHN
    // và kích thước gói hàng được suy ra từ giỏ hàng trong session.
    [HttpPost]
    public async Task<IActionResult> CalculateShippingAjax([FromBody] CalculateShippingRequest request)
    {
        var sessionId = GetSessionId();
        var cart = await _cartService.GetCartAsync(sessionId, request.District ?? string.Empty);

        var shippingInfo = await _shippingService.CalculateShippingAsync(
            cart.Subtotal,
            request.District ?? string.Empty,
            request.GhnDistrictId,
            request.GhnWardCode,
            cart.ShippingPackage);

        return Json(new
        {
            success = true,
            shippingInfo = new
            {
                shippingFee = shippingInfo.ShippingFee,
                zone = (int)shippingInfo.Zone,
                isFreeShipping = shippingInfo.IsFreeShipping,
                isReducedShipping = shippingInfo.IsReducedShipping,
                amountToFreeShipping = shippingInfo.AmountToFreeShipping,
                message = shippingInfo.Message
            }
        });
    }

    // Request DTO cho cập nhật số lượng
    public class UpdateQuantityRequest
    {
        public int CartItemId { get; set; }
        public int Quantity { get; set; }
    }

    // Request DTO cho xóa sản phẩm
    public class RemoveFromCartRequest
    {
        public int CartItemId { get; set; }
    }
    
    // Request DTO cho tính phí ship
    public class CalculateShippingRequest
    {
        public string? District { get; set; }
        public int? GhnDistrictId { get; set; }
        public string? GhnWardCode { get; set; }
    }

    // POST: Áp dụng mã giảm giá bằng AJAX — gọi cart service apply coupon, trả về giỏ hàng mới
    [HttpPost]
    public async Task<IActionResult> ApplyCouponAjax([FromBody] ApplyCouponAjaxRequest request)
    {
        var sessionId = GetSessionId();
        var result = await _cartService.ApplyCouponAsync(sessionId, request.CouponCode ?? "");

        if (!result.Success)
            return Json(new { success = false, message = result.ErrorMessage });

        var cart = await _cartService.GetCartAsync(sessionId);

        return Json(new
        {
            success      = true,
            message      = result.Message,
            couponCode   = result.CouponCode,
            discount     = cart.Discount,
            subtotal     = cart.Subtotal,
            shippingFee  = cart.ShippingFee,
            total        = cart.Total
        });
    }

    // Request DTO cho áp dụng coupon
    public class ApplyCouponAjaxRequest
    {
        public string? CouponCode { get; set; }
    }

    // POST: Áp dụng mã giảm giá (form submit) — redirect về trang trước hoặc giỏ hàng
    [HttpPost]
    public async Task<IActionResult> ApplyCoupon(string couponCode, string? returnUrl = null)
    {
        var sessionId = GetSessionId();
        var result = await _cartService.ApplyCouponAsync(sessionId, couponCode);

        if (result.Success)
            TempData["CouponSuccess"] = result.Message;
        else
            TempData["CouponError"] = result.ErrorMessage;

        // Redirect về returnUrl nếu hợp lệ (chống open redirect)
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    // POST: Bỏ mã giảm giá
    [HttpPost]
    public async Task<IActionResult> RemoveCoupon(string? returnUrl = null)
    {
        var sessionId = GetSessionId();
        await _cartService.RemoveCouponAsync(sessionId);
        TempData["CouponSuccess"] = "Đã bỏ mã giảm giá";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    // GET: Lấy danh sách mã giảm giá khả dụng (dựa trên subtotal + số lượng item)
    [HttpGet]
    public async Task<IActionResult> GetAvailableCoupons()
    {
        var sessionId = GetSessionId();
        var cart = await _cartService.GetCartAsync(sessionId);
        var subtotal = cart.Subtotal;
        var itemCount = cart.Items.Sum(i => i.Quantity);

        var coupons = await _couponService.GetAvailableCouponsAsync(subtotal, itemCount);

        return Json(coupons.Select(c => new
        {
            id             = c.Id,
            code           = c.Code,
            type           = c.Type == Fruitables.Models.CouponType.Percentage ? "percent" : "fixed",
            value          = c.Value,
            discountAmount = c.DiscountAmount,
            minOrderAmount = c.MinOrderAmount,
            minQuantity    = c.MinQuantity,
            endDate        = c.EndDate?.ToString("dd/MM/yyyy"),
            isEligible     = c.IsEligible,
            reason         = c.IneligibleReason
        }));
    }

    // Helper: lấy/tạo SessionId — cho phép guest user có giỏ hàng riêng (lưu trong session server)
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
