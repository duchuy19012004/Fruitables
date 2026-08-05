using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Services.Orders.Cart;

namespace Fruitables.Controllers;

// Controller trang liên hệ: hiển thị form + gửi tin nhắn.
public class ContactController : Controller
{
    private readonly IContactService _contactService;
    private readonly ICartService _cartService;

    // Inject 2 service: contact (gửi tin nhắn), cart (đếm giỏ hàng)
    public ContactController(IContactService contactService, ICartService cartService)
    {
        _contactService = contactService;
        _cartService = cartService;
    }

    // GET: Hiển thị form liên hệ
    public async Task<IActionResult> Index()
    {
        var sessionId = GetSessionId();
        ViewBag.CartCount = await _cartService.GetCartCountAsync(sessionId);
        return View();
    }

    // POST: Gửi tin nhắn liên hệ
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(string name, string email, string message)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(message))
        {
            TempData["Error"] = "Please fill in all fields.";
            return RedirectToAction(nameof(Index));
        }

        await _contactService.SendMessageAsync(name, email, message);
        TempData["Success"] = "Your message has been sent successfully!";
        return RedirectToAction(nameof(Index));
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
