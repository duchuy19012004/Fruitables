using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Controllers;

public class ChatController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
