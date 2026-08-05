using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Attributes;
using System.Security.Claims;
using Fruitables.Services.Identity.Rbac;

namespace Fruitables.Areas.Admin.Controllers;

/// <summary>
/// Controller for Permission Management in Admin Panel
/// Requirements: 10.4
/// </summary>
[Area("Admin")]
[RequirePermission("system.manage_rbac")]
public class PermissionController : Controller
{
    private readonly IRbacService _rbacService;
    private readonly ILogger<PermissionController> _logger;

    public PermissionController(IRbacService rbacService, ILogger<PermissionController> logger)
    {
        _rbacService = rbacService;
        _logger = logger;
    }

    /// <summary>
    /// Display list of permissions grouped by module with optional filter
    /// GET: /Admin/Permission
    /// Requirements: 10.4 - Display permission list
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? module)
    {
        try
        {
            Dictionary<string, List<Models.Permission>> permissionsGrouped;

            if (!string.IsNullOrWhiteSpace(module))
            {
                // Filter by specific module
                var permissions = await _rbacService.GetPermissionsByModuleAsync(module);
                permissionsGrouped = new Dictionary<string, List<Models.Permission>>
                {
                    { module, permissions }
                };
            }
            else
            {
                // Get all permissions grouped by module
                permissionsGrouped = await _rbacService.GetPermissionsGroupedByModuleAsync();
            }

            ViewBag.SelectedModule = module;
            ViewBag.AllModules = permissionsGrouped.Keys.OrderBy(k => k).ToList();

            return View(permissionsGrouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading permissions list");
            TempData["Error"] = "Có lỗi xảy ra khi tải danh sách quyền hạn";
            return View(new Dictionary<string, List<Models.Permission>>());
        }
    }

    /// <summary>
    /// Display create permission form
    /// GET: /Admin/Permission/Create
    /// Requirements: 10.4 - Create permission
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    /// <summary>
    /// Create a new permission
    /// POST: /Admin/Permission/Create
    /// Requirements: 10.4 - Create permission
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string module, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên quyền hạn không được để trống";
                ViewBag.Name = name;
                ViewBag.Module = module;
                ViewBag.Description = description;
                return View();
            }

            if (string.IsNullOrWhiteSpace(module))
            {
                TempData["Error"] = "Module không được để trống";
                ViewBag.Name = name;
                ViewBag.Module = module;
                ViewBag.Description = description;
                return View();
            }

            var adminId = GetCurrentAdminId();
            var permission = await _rbacService.CreatePermissionAsync(name, module, description, adminId);

            TempData["Success"] = "Tạo quyền hạn thành công";
            return RedirectToAction(nameof(Index), new { module = permission.Module });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("format"))
        {
            _logger.LogWarning(ex, "Failed to create permission with invalid format: {Name}", name);
            TempData["Error"] = "Tên quyền hạn phải theo format module.action (ví dụ: products.view)";
            ViewBag.Name = name;
            ViewBag.Module = module;
            ViewBag.Description = description;
            return View();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create permission: {Name}", name);
            TempData["Error"] = ex.Message == "Permission name already exists"
                ? "Tên quyền hạn đã tồn tại. Vui lòng chọn tên khác."
                : ex.Message;
            ViewBag.Name = name;
            ViewBag.Module = module;
            ViewBag.Description = description;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission: {Name}", name);
            TempData["Error"] = "Có lỗi xảy ra khi tạo quyền hạn";
            ViewBag.Name = name;
            ViewBag.Module = module;
            ViewBag.Description = description;
            return View();
        }
    }

    /// <summary>
    /// Delete a permission
    /// POST: /Admin/Permission/Delete/{id}
    /// Requirements: 10.4 - Delete permission
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var permission = await _rbacService.GetPermissionByIdAsync(id);
            if (permission == null)
            {
                TempData["Error"] = "Không tìm thấy quyền hạn";
                return RedirectToAction(nameof(Index));
            }

            var module = permission.Module;
            var adminId = GetCurrentAdminId();
            await _rbacService.DeletePermissionAsync(id, adminId);

            TempData["Success"] = "Xóa quyền hạn thành công";
            return RedirectToAction(nameof(Index), new { module });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete permission: {PermissionId}", id);
            TempData["Error"] = ex.Message == "Cannot delete permission that is assigned to roles"
                ? "Không thể xóa quyền hạn đang được gán cho vai trò. Vui lòng xóa quyền hạn khỏi tất cả vai trò trước."
                : ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting permission: {PermissionId}", id);
            TempData["Error"] = "Có lỗi xảy ra khi xóa quyền hạn";
            return RedirectToAction(nameof(Index));
        }
    }

    #region Helper Methods

    /// <summary>
    /// Get current admin user ID from claims
    /// </summary>
    private int GetCurrentAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var id) ? id : 0;
    }

    #endregion
}
