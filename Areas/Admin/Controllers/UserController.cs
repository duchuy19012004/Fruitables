using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Models;
using Fruitables.Attributes;
using System.Security.Claims;
using Fruitables.Services.Identity.Rbac;
using Fruitables.Services.Identity.Users;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class UserController : Controller
{
    private readonly IUserManagementService _userManagementService;
    private readonly IRbacService _rbacService;

    public UserController(
        IUserManagementService userManagementService,
        IRbacService rbacService)
    {
        _userManagementService = userManagementService;
        _rbacService = rbacService;
    }

    public async Task<IActionResult> Index(
        string? searchTerm,
        bool? isActive,
        string? roleType,
        string sortBy = "CreatedAt",
        bool sortDescending = true,
        int page = 1)
    {
        var filter = new UserFilterRequest
        {
            SearchTerm = searchTerm,
            IsActive = isActive,
            RoleType = roleType,
            SortBy = sortBy,
            SortDescending = sortDescending,
            Page = page,
            PageSize = 20
        };

        var result = await _userManagementService.GetCustomersAsync(filter);

        ViewBag.SearchTerm = searchTerm;
        ViewBag.IsActive = isActive;
        ViewBag.RoleType = roleType;
        ViewBag.SortBy = sortBy;
        ViewBag.SortDescending = sortDescending;
        ViewBag.CanLockAccount = _userManagementService.CanLockAccount(GetCurrentUserRole());

        return View(result);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var result = await _userManagementService.GetCustomerDetailAsync(id);

        if (!result.IsValid)
        {
            TempData["Error"] = result.ErrorMessage ?? "Không tìm thấy khách hàng";
            return RedirectToAction(nameof(Index));
        }

        var currentUserRole = GetCurrentUserRole();
        ViewBag.CanLockAccount = _userManagementService.CanLockAccount(currentUserRole);
        ViewBag.CurrentUserRole = currentUserRole;
        ViewBag.CurrentAdminId = GetCurrentAdminId();

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> PurchaseHistory(
        int customerId,
        OrderStatus? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 10)
    {
        var result = await _userManagementService.GetPurchaseHistoryAsync(
            customerId,
            status,
            startDate,
            endDate,
            page,
            pageSize);

        if (!result.IsValid)
        {
            return Json(new { isValid = false, errorMessage = result.ErrorMessage });
        }

        return Json(new { isValid = true, data = result.Data });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock([FromBody] LockAccountRequest request)
    {
        request.AdminId = GetCurrentAdminId();
        request.AdminRole = GetCurrentUserRole();
        request.IpAddress = GetClientIpAddress();
        request.UserAgent = Request.Headers["User-Agent"].ToString();

        var result = await _userManagementService.LockAccountAsync(request);

        if (!result.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                error = result.ErrorMessage,
                errorCode = result.ErrorCode
            });
        }

        return Json(new
        {
            success = true,
            data = result.Data
        });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock([FromBody] UnlockAccountRequest request)
    {
        request.AdminId = GetCurrentAdminId();
        request.AdminRole = GetCurrentUserRole();
        request.IpAddress = GetClientIpAddress();
        request.UserAgent = Request.Headers["User-Agent"].ToString();

        var result = await _userManagementService.UnlockAccountAsync(request);

        if (!result.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                error = result.ErrorMessage,
                errorCode = result.ErrorCode
            });
        }

        return Json(new
        {
            success = true,
            message = "Mở khóa tài khoản thành công"
        });
    }

    [HttpGet]
    public async Task<IActionResult> AccountLogs(int customerId)
    {
        var result = await _userManagementService.GetAccountLogsAsync(customerId);

        if (!result.IsValid)
        {
            return Json(new { isValid = false, errorMessage = result.ErrorMessage });
        }

        return Json(new { isValid = true, data = result.Data });
    }

    [HttpGet]
    [RequirePermission("users.update")]
    public async Task<IActionResult> GetUserRoles(int userId)
    {
        try
        {
            var roles = await _rbacService.GetUserRolesAsync(userId);
            return Json(new
            {
                success = true,
                data = roles
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [HttpGet]
    [RequirePermission("users.update")]
    public async Task<IActionResult> GetAvailableRoles(int userId)
    {
        try
        {
            var allRoles = await _rbacService.GetAllRolesAsync(includeInactive: false);
            var userRoles = await _rbacService.GetUserRolesAsync(userId);
            var userRoleIds = userRoles.Select(r => r.Id).ToHashSet();
            
            var availableRoles = allRoles.Where(r => !userRoleIds.Contains(r.Id)).ToList();
            
            return Json(new
            {
                success = true,
                data = availableRoles
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [HttpPost]
    [RequirePermission("users.update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        try
        {
            var adminId = GetCurrentAdminId();
            await _rbacService.AssignRoleToUserAsync(request.UserId, request.RoleId, adminId);

            return Json(new
            {
                success = true,
                message = "Gán vai trò thành công"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [HttpPost]
    [RequirePermission("users.update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeRole([FromBody] RevokeRoleRequest request)
    {
        try
        {
            var adminId = GetCurrentAdminId();
            await _rbacService.RevokeRoleFromUserAsync(request.UserId, request.RoleId, adminId);

            return Json(new
            {
                success = true,
                message = "Thu hồi vai trò thành công"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [HttpGet]
    [RequirePermission("users.update")]
    public async Task<IActionResult> GetEffectivePermissions(int userId)
    {
        try
        {
            var permissions = await _rbacService.GetUserPermissionsAsync(userId);
            var permissionsGrouped = await _rbacService.GetPermissionsGroupedByModuleAsync();
            
            var effectiveByModule = new Dictionary<string, List<string>>();
            foreach (var permission in permissions)
            {
                var parts = permission.Split('.');
                if (parts.Length == 2)
                {
                    var module = parts[0];
                    if (!effectiveByModule.ContainsKey(module))
                    {
                        effectiveByModule[module] = new List<string>();
                    }
                    effectiveByModule[module].Add(permission);
                }
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    permissions = permissions,
                    groupedByModule = effectiveByModule
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private int GetCurrentAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var id) ? id : 0;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";
    }

    private string? GetClientIpAddress()
    {
        // Check for forwarded IP first (behind proxy/load balancer)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
