using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Attributes;
using Fruitables.Services.Identity.Rbac;

namespace Fruitables.Areas.Admin.Controllers;

/// <summary>
/// Controller for RBAC Audit Log Management in Admin Panel
/// Requirements: 10.7
/// </summary>
[Area("Admin")]
[RequirePermission("system.view_logs")]
public class RbacAuditController : Controller
{
    private readonly IRbacService _rbacService;
    private readonly ILogger<RbacAuditController> _logger;

    public RbacAuditController(IRbacService rbacService, ILogger<RbacAuditController> logger)
    {
        _rbacService = rbacService;
        _logger = logger;
    }

    /// <summary>
    /// Display audit logs with filtering and pagination
    /// GET: /Admin/RbacAudit
    /// Requirements: 10.7 - Display audit logs with filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? entityType,
        int? entityId,
        int? changedBy,
        DateTime? startDate,
        DateTime? endDate,
        int page = 1)
    {
        try
        {
            const int pageSize = 20;

            var (logs, totalCount) = await _rbacService.GetAuditLogsAsync(
                page,
                pageSize,
                entityType,
                entityId,
                changedBy,
                startDate,
                endDate);

            // Pass filter values to view
            ViewBag.EntityType = entityType;
            ViewBag.EntityId = entityId;
            ViewBag.ChangedBy = changedBy;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            return View(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audit logs");
            TempData["Error"] = "Có lỗi xảy ra khi tải nhật ký kiểm toán";
            return View(new List<Models.RbacAuditLog>());
        }
    }
}
