using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Services.Communications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Fruitables.Services.Identity.Rbac;

namespace Fruitables.Filters;

// Authorization filter kiểm tra quyền truy cập dựa trên RequirePermissionAttribute.
// Hỗ trợ AND (cần tất cả) và OR (cần ít nhất một) logic.
public class RequirePermissionFilter : IAsyncAuthorizationFilter
{
    private readonly IRbacService _rbacService;
    private readonly ILogger<RequirePermissionFilter> _logger;

    public RequirePermissionFilter(
        IRbacService rbacService,
        ILogger<RequirePermissionFilter> logger)
    {
        _rbacService = rbacService;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Lấy danh sách RequirePermissionAttribute từ endpoint metadata
        var permissionAttributes = context.ActionDescriptor.EndpointMetadata
            .OfType<RequirePermissionAttribute>()
            .ToList();

        // Không có attribute → cho phép truy cập
        if (!permissionAttributes.Any())
        {
            return;
        }

        // Kiểm tra user đã đăng nhập chưa
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated user attempted to access {Action}",
                context.ActionDescriptor.DisplayName);
            
            context.Result = new UnauthorizedResult();
            return;
        }

        // Lấy userId từ claims
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            _logger.LogError("User is authenticated but has no valid user ID claim");
            context.Result = new UnauthorizedResult();
            return;
        }

        // Duyệt từng attribute (nhiều attribute → logic AND giữa chúng)
        foreach (var attribute in permissionAttributes)
        {
            bool hasPermission;

            if (attribute.Logic == PermissionLogic.Or)
            {
                // User cần có ít nhất 1 trong các quyền
                hasPermission = await _rbacService.HasAnyPermissionAsync(userId, attribute.Permissions);
            }
            else // PermissionLogic.And
            {
                // User cần có tất cả quyền
                hasPermission = await _rbacService.HasAllPermissionsAsync(userId, attribute.Permissions);
            }

            if (!hasPermission)
            {
                var permissionsStr = string.Join(", ", attribute.Permissions);
                var logicStr = attribute.Logic == PermissionLogic.Or ? "any of" : "all of";
                
                _logger.LogWarning(
                    "User {UserId} denied access to {Action}. Required {Logic}: {Permissions}",
                    userId,
                    context.ActionDescriptor.DisplayName,
                    logicStr,
                    permissionsStr);

                // Ghi log audit
                await LogAccessDeniedAsync(userId, context.ActionDescriptor.DisplayName ?? "Unknown", permissionsStr);

                context.Result = new ForbidResult();
                return;
            }
        }

        // Tất cả kiểm tra đều pass
        _logger.LogDebug("User {UserId} granted access to {Action}", userId, context.ActionDescriptor.DisplayName);
    }

    // Ghi log khi truy cập bị từ chối
    private async Task LogAccessDeniedAsync(int userId, string action, string requiredPermissions)
    {
        try
        {
            _logger.LogWarning(
                "Access denied: User {UserId} attempted to access {Action} without required permissions: {Permissions}",
                userId,
                action,
                requiredPermissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log access denied event for user {UserId}", userId);
        }
    }
}
