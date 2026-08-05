using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Services.Catalog.Combos;
using Fruitables.Services.Orders.Cart;

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
    [ValidateAntiForgeryToken]
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
