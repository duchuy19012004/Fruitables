using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Attributes;
using System.Security.Claims;
using Fruitables.Services.Identity.Rbac;
using Fruitables.Services.Infrastructure;

namespace Fruitables.Areas.Admin.Controllers;

/// <summary>
/// Controller for Role Management in Admin Panel
/// Requirements: 10.1
/// </summary>
[Area("Admin")]
[RequirePermission("system.manage_rbac")]
public class RoleController : Controller
{
    private readonly IRbacService _rbacService;
    private readonly IMigrationService _migrationService;
    private readonly ILogger<RoleController> _logger;

    public RoleController(
        IRbacService rbacService, 
        IMigrationService migrationService,
        ILogger<RoleController> logger)
    {
        _rbacService = rbacService;
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// Display list of roles with pagination and search
    /// GET: /Admin/Role
    /// Requirements: 10.1 - Display role list
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int page = 1)
    {
        try
        {
            var (roles, totalCount) = await _rbacService.GetRolesPagedAsync(page, 20, searchTerm);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);
            ViewBag.TotalCount = totalCount;

            return View(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading roles list");
            TempData["Error"] = "Có lỗi xảy ra khi tải danh sách vai trò";
            return View(new List<Models.Role>());
        }
    }

    /// <summary>
    /// Display role details with permissions
    /// GET: /Admin/Role/Detail/{id}
    /// Requirements: 10.2 - Display role details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        try
        {
            var role = await _rbacService.GetRoleByIdAsync(id);
            if (role == null)
            {
                TempData["Error"] = "Không tìm thấy vai trò";
                return RedirectToAction(nameof(Index));
            }

            var permissions = await _rbacService.GetRolePermissionsAsync(id);
            var allPermissions = await _rbacService.GetAllPermissionsAsync();
            
            ViewBag.Permissions = permissions;
            ViewBag.RolePermissions = permissions;
            ViewBag.AllPermissions = allPermissions;

            return View(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading role detail for ID {RoleId}", id);
            TempData["Error"] = "Có lỗi xảy ra khi tải thông tin vai trò";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Display create role form
    /// GET: /Admin/Role/Create
    /// Requirements: 10.1 - Create role
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    /// <summary>
    /// Create a new role
    /// POST: /Admin/Role/Create
    /// Requirements: 10.1 - Create role
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên vai trò không được để trống";
                ViewBag.Name = name;
                ViewBag.Description = description;
                return View();
            }

            var adminId = GetCurrentAdminId();
            var role = await _rbacService.CreateRoleAsync(name, description, adminId);

            TempData["Success"] = "Tạo vai trò thành công";
            return RedirectToAction(nameof(Detail), new { id = role.Id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create role: {Name}", name);
            TempData["Error"] = ex.Message == "Role name already exists" 
                ? "Tên vai trò đã tồn tại. Vui lòng chọn tên khác." 
                : ex.Message;
            ViewBag.Name = name;
            ViewBag.Description = description;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role: {Name}", name);
            TempData["Error"] = "Có lỗi xảy ra khi tạo vai trò";
            ViewBag.Name = name;
            ViewBag.Description = description;
            return View();
        }
    }

    /// <summary>
    /// Display edit role form
    /// GET: /Admin/Role/Edit/{id}
    /// Requirements: 10.1 - Edit role
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var role = await _rbacService.GetRoleByIdAsync(id);
            if (role == null)
            {
                TempData["Error"] = "Không tìm thấy vai trò";
                return RedirectToAction(nameof(Index));
            }

            return View(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading role for edit: {RoleId}", id);
            TempData["Error"] = "Có lỗi xảy ra khi tải thông tin vai trò";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Update an existing role
    /// POST: /Admin/Role/Edit/{id}
    /// Requirements: 10.1 - Edit role
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên vai trò không được để trống";
                var role = await _rbacService.GetRoleByIdAsync(id);
                return View(role);
            }

            var adminId = GetCurrentAdminId();
            await _rbacService.UpdateRoleAsync(id, name, description, adminId);

            TempData["Success"] = "Cập nhật vai trò thành công";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update role: {RoleId}", id);
            TempData["Error"] = ex.Message == "Role name already exists" 
                ? "Tên vai trò đã tồn tại. Vui lòng chọn tên khác." 
                : ex.Message;
            var role = await _rbacService.GetRoleByIdAsync(id);
            return View(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role: {RoleId}", id);
            TempData["Error"] = "Có lỗi xảy ra khi cập nhật vai trò";
            var role = await _rbacService.GetRoleByIdAsync(id);
            return View(role);
        }
    }

    /// <summary>
    /// Delete a role
    /// POST: /Admin/Role/Delete/{id}
    /// Requirements: 10.1 - Delete role
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var adminId = GetCurrentAdminId();
            await _rbacService.DeleteRoleAsync(id, adminId);

            TempData["Success"] = "Xóa vai trò thành công";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete role: {RoleId}", id);
            TempData["Error"] = ex.Message == "Cannot delete role that is assigned to users"
                ? "Không thể xóa vai trò đang được gán cho người dùng. Vui lòng xóa tất cả người dùng khỏi vai trò này trước."
                : ex.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role: {RoleId}", id);
            TempData["Error"] = "Có lỗi xảy ra khi xóa vai trò";
            return RedirectToAction(nameof(Detail), new { id });
        }
    }

    /// <summary>
    /// Toggle role active status
    /// POST: /Admin/Role/ToggleActive
    /// Requirements: 10.1 - Toggle role active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, bool isActive)
    {
        try
        {
            var adminId = GetCurrentAdminId();
            await _rbacService.ToggleRoleActiveAsync(id, isActive, adminId);

            return Json(new
            {
                success = true,
                message = isActive ? "Kích hoạt vai trò thành công" : "Vô hiệu hóa vai trò thành công"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling role active status: {RoleId}", id);
            return Json(new
            {
                success = false,
                error = "Có lỗi xảy ra khi thay đổi trạng thái vai trò"
            });
        }
    }
    /// <summary>
    /// Assign permissions to a role
    /// POST: /Admin/Role/AssignPermissions/{id}
    /// Requirements: 10.3 - Assign permissions to role
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPermissions(int id, List<int> permissionIds)
    {
        try
        {
            var adminId = GetCurrentAdminId();

            // Get current role permissions
            var currentPermissions = await _rbacService.GetRolePermissionsAsync(id);
            var currentPermissionIds = currentPermissions.Select(p => p.Id).ToHashSet();

            // Determine permissions to add and remove
            var permissionsToAdd = permissionIds.Except(currentPermissionIds).ToList();
            var permissionsToRemove = currentPermissionIds.Except(permissionIds).ToList();

            // Add new permissions
            if (permissionsToAdd.Any())
            {
                await _rbacService.AssignPermissionsToRoleAsync(id, permissionsToAdd, adminId);
            }

            // Remove revoked permissions
            foreach (var permissionId in permissionsToRemove)
            {
                await _rbacService.RevokePermissionFromRoleAsync(id, permissionId, adminId);
            }

            TempData["Success"] = "Cập nhật quyền hạn cho vai trò thành công";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permissions to role: {RoleId}", id);
            TempData["Error"] = "Có lỗi xảy ra khi cập nhật quyền hạn";
            return RedirectToAction(nameof(Detail), new { id });
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

    #region Migration Actions

    /// <summary>
    /// Display migration status page
    /// GET: /Admin/Role/MigrationStatus
    /// Requirements: 7.4 - Migration status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MigrationStatus()
    {
        try
        {
            var status = await _migrationService.GetMigrationStatusAsync();
            return View(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration status");
            TempData["Error"] = "Có lỗi xảy ra khi lấy trạng thái migration";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Run RBAC migration
    /// POST: /Admin/Role/RunMigration
    /// Requirements: 7.4 - Run migration to convert existing users
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunMigration()
    {
        try
        {
            _logger.LogInformation("Starting RBAC migration from admin panel");
            
            var result = await _migrationService.MigrateToRbacAsync();

            if (result.Success)
            {
                TempData["Success"] = $"Migration thành công! Đã chuyển đổi {result.UsersProcessed} người dùng.";
            }
            else
            {
                var errorMessage = string.Join(", ", result.Errors);
                TempData["Error"] = $"Migration thất bại: {errorMessage}";
            }

            return RedirectToAction(nameof(MigrationStatus));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running RBAC migration");
            TempData["Error"] = "Có lỗi xảy ra khi chạy migration";
            return RedirectToAction(nameof(MigrationStatus));
        }
    }

    #endregion
}
