using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ChatLogController : Controller
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatLogController> _logger;

    public ChatLogController(IChatService chatService, ILogger<ChatLogController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    // GET: Admin/ChatLog
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 20;

        try
        {
            var (items, totalCount) = await _chatService.GetSessionsPageAsync(page, pageSize, ct);

            ViewBag.CurrentPage = page < 1 ? 1 : page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            return View(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading chat session logs");
            TempData["Error"] = "Có lỗi xảy ra khi tải nhật ký chat";
            ViewBag.CurrentPage = 1;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = 0;
            ViewBag.TotalPages = 1;
            return View(new List<ChatSessionListItem>());
        }
    }

    // GET: Admin/ChatLog/Detail/{id}
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct = default)
    {
        var session = await _chatService.GetSessionWithMessagesAsync(id, ct);
        if (session is null)
        {
            TempData["Error"] = "Không tìm thấy phiên chat";
            return RedirectToAction(nameof(Index));
        }

        return View(session);
    }
}
