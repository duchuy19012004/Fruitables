using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;

namespace Fruitables.Controllers;

// Controller trang chủ: hiển thị sản phẩm, danh mục, testimonials.
public class HomeController : Controller
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ITestimonialService _testimonialService;
    private readonly ICartService _cartService;

    // Inject 4 service: product, category (tabs), testimonial (khách hàng nói gì), cart (đếm giỏ)
    public HomeController(
        IProductService productService,
        ICategoryService categoryService,
        ITestimonialService testimonialService,
        ICartService cartService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _testimonialService = testimonialService;
        _cartService = cartService;
    }

    // GET: Trang chủ — load tất cả sản phẩm, category cha (tabs), testimonials đang active
    public async Task<IActionResult> Index()
    {
        var sessionId = GetSessionId();
        ViewBag.CartCount = await _cartService.GetCartCountAsync(sessionId);
        ViewBag.AllProducts = await _productService.GetAllProductsAsync();
        ViewBag.Categories = await _categoryService.GetParentCategoriesAsync();
        ViewBag.Testimonials = await _testimonialService.GetActiveTestimonialsAsync();
        return View();
    }

    // GET: Trang chính sách bảo mật
    public IActionResult Privacy()
    {
        return View();
    }

    // GET: Trang lỗi chung (không cache)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
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
}
