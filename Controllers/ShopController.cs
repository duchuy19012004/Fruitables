using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using System.Security.Claims;
using Fruitables.Services.Catalog.Categories;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Orders.Cart;
using Fruitables.Services.Reviews;

namespace Fruitables.Controllers;

// Controller cửa hàng: danh sách sản phẩm (có lọc/tìm kiếm/phân trang) và chi tiết sản phẩm.
public class ShopController : Controller
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ICartService _cartService;
    private readonly IReviewService _reviewService;

    // Inject 4 service: product, category (tree + filter), cart (đếm giỏ hàng), review (hiển thị đánh giá)
    public ShopController(
        IProductService productService,
        ICategoryService categoryService,
        ICartService cartService,
        IReviewService reviewService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _cartService = cartService;
        _reviewService = reviewService;
    }

    // GET: /Shop — danh sách sản phẩm với lọc (category, search, price, sort) + phân trang
    public async Task<IActionResult> Index(int? categoryId, string? search, decimal? minPrice, decimal? maxPrice, string? sortBy, int page = 1)
    {
        var sessionId = GetSessionId();
        ViewBag.CartCount = await _cartService.GetCartCountAsync(sessionId);

        var viewModel = await _productService.GetShopViewModelAsync(categoryId, search, minPrice, maxPrice, sortBy, page, 9);
        if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return PartialView("_ShopCatalog", viewModel);
        return View(viewModel);
    }

    // GET: /Shop/Detail/{id} — redirect từ ID cũ sang slug mới (301 permanent)
    [Route("Shop/Detail/{id:int}")]
    public async Task<IActionResult> DetailById(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound();
        return RedirectPermanent($"/Shop/Detail/{product.Slug}");
    }

    // GET: /Shop/Detail/{slug} — chi tiết sản phẩm + related products + review
    [Route("Shop/Detail/{slug}")]
    public async Task<IActionResult> Detail(string? slug, int page = 1, string? sortBy = null)
    {
        if (string.IsNullOrEmpty(slug)) return NotFound();

        var sessionId = GetSessionId();
        ViewBag.CartCount = await _cartService.GetCartCountAsync(sessionId);

        var product = await _productService.GetProductBySlugAsync(slug);
        if (product == null) return NotFound();

        var id = product.Id;

        ViewBag.RelatedProducts = await _productService.GetRelatedProductsAsync(id, 4);
        ViewBag.CategoryTree = await _categoryService.GetCategoryTreeAsync();
        ViewBag.FeaturedProducts = await _productService.GetFeaturedProductsAsync(3);

        // Load thông tin đánh giá
        var userId = GetCurrentUserId();
        
        // Thống kê đánh giá (sao, số lượng)
        var statistics = await _reviewService.GetProductReviewStatisticsAsync(id);
        ViewBag.ReviewStatistics = statistics;

        // Parse sortBy string sang enum
        ReviewSortBy sortByEnum = ReviewSortBy.Newest;
        if (!string.IsNullOrEmpty(sortBy) && Enum.TryParse<ReviewSortBy>(sortBy, true, out var parsedSort))
        {
            sortByEnum = parsedSort;
        }

        // Danh sách review có phân trang
        var reviewFilter = new ReviewFilterDto
        {
            ProductId = id,
            Page = page,
            PageSize = 10,
            SortBy = sortByEnum
        };
        var reviews = await _reviewService.GetProductReviewsAsync(reviewFilter, userId);
        ViewBag.Reviews = reviews;

        // Kiểm tra user hiện tại có được review không
        ViewBag.CanReview = false;
        if (userId > 0)
        {
            ViewBag.CanReview = await _reviewService.CanUserReviewProductAsync(userId, id);
        }

        return View(product);
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

    // Helper: lấy userId từ claims, trả 0 nếu anonymous
    private int GetCurrentUserId()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
        }
        return 0;
    }
}
