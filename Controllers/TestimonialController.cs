using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Services.Orders.Cart;
using Fruitables.Services.Reviews;

namespace Fruitables.Controllers;

// Controller hiển thị trang testimonials (cảm nhận khách hàng).
public class TestimonialController : Controller
{
    private readonly ITestimonialService _testimonialService;
    private readonly ICartService _cartService;

    public TestimonialController(ITestimonialService testimonialService, ICartService cartService)
    {
        _testimonialService = testimonialService;
        _cartService = cartService;
    }

    // GET: Danh sách testimonials đang active
    public async Task<IActionResult> Index()
    {
        var sessionId = GetSessionId();
        ViewBag.CartCount = await _cartService.GetCartCountAsync(sessionId);
        var testimonials = await _testimonialService.GetActiveTestimonialsAsync();
        return View(testimonials);
    }

    // Helper: lấy/tạo SessionId
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
