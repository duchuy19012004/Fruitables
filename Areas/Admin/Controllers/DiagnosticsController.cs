using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Services.Communications;
using System.Security.Claims;
using Fruitables.Services.Identity.Rbac;
using Fruitables.Services.Infrastructure;
using Fruitables.Services.Infrastructure.DatabaseConsolidation;
using Fruitables.Attributes;

namespace Fruitables.Areas.Admin.Controllers;

/// <summary>
/// Controller tạm thời để chẩn đoán vấn đề RBAC và Order History
/// </summary>
[Area("Admin")]
[Authorize]
public class DiagnosticsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMigrationService _migrationService;
    private readonly IRbacService _rbacService;
    private readonly IDatabaseConsolidationService? _databaseConsolidationService;

    public DiagnosticsController(
        ApplicationDbContext context,
        IMigrationService migrationService,
        IRbacService rbacService,
        IDatabaseConsolidationService? databaseConsolidationService = null)
    {
        _context = context;
        _migrationService = migrationService;
        _rbacService = rbacService;
        _databaseConsolidationService = databaseConsolidationService;
    }

    /// <summary>
    /// Trang chẩn đoán chính
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Content("Không thể lấy User ID từ claims");
        }

        var diagnostics = new System.Text.StringBuilder();
        diagnostics.AppendLine("=== CHẨN ĐOÁN HỆ THỐNG ===\n");

        // 1. Kiểm tra Migration Status
        diagnostics.AppendLine("1. RBAC MIGRATION STATUS:");
        var migrationStatus = await _migrationService.GetMigrationStatusAsync();
        diagnostics.AppendLine($"   - Đã hoàn thành: {migrationStatus.IsCompleted}");
        diagnostics.AppendLine($"   - Tổng số users: {migrationStatus.TotalUsers}");
        diagnostics.AppendLine($"   - Users đã migrate: {migrationStatus.MigratedUsers}");
        diagnostics.AppendLine($"   - Ngày migrate cuối: {migrationStatus.LastMigrationDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Chưa có"}\n");

        // 2. Kiểm tra User hiện tại
        diagnostics.AppendLine("2. THÔNG TIN USER HIỆN TẠI:");
        var currentUser = await _context.Users
            .Include(u => u.UserRoleMappings)
            .ThenInclude(urm => urm.Role)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser != null)
        {
            diagnostics.AppendLine($"   - User ID: {currentUser.Id}");
            diagnostics.AppendLine($"   - Name: {currentUser.Name}");
            diagnostics.AppendLine($"   - Email: {currentUser.Email}");
            diagnostics.AppendLine($"   - Legacy Role: {currentUser.Role}");
            diagnostics.AppendLine($"   - RBAC Roles: {currentUser.UserRoleMappings.Count}");
            
            foreach (var mapping in currentUser.UserRoleMappings)
            {
                diagnostics.AppendLine($"     * {mapping.Role?.Name ?? "Unknown"} (ID: {mapping.RoleId})");
            }
        }
        else
        {
            diagnostics.AppendLine("   - User không tìm thấy!");
        }
        diagnostics.AppendLine();

        // 3. Kiểm tra Permissions của user
        diagnostics.AppendLine("3. PERMISSIONS CỦA USER:");
        var permissions = await _rbacService.GetUserPermissionsAsync(userId.Value);
        diagnostics.AppendLine($"   - Tổng số permissions: {permissions.Count}");
        
        var hasManageRbac = permissions.Contains("system.manage_rbac");
        diagnostics.AppendLine($"   - Có quyền 'system.manage_rbac': {hasManageRbac}");
        
        if (permissions.Count > 0)
        {
            diagnostics.AppendLine("   - Danh sách permissions:");
            foreach (var perm in permissions.Take(10))
            {
                diagnostics.AppendLine($"     * {perm}");
            }
            if (permissions.Count > 10)
            {
                diagnostics.AppendLine($"     ... và {permissions.Count - 10} permissions khác");
            }
        }
        diagnostics.AppendLine();

        // 4. Kiểm tra Orders của user
        diagnostics.AppendLine("4. LỊCH SỬ ĐƠN HÀNG:");
        var orders = await _context.Orders
            .Where(o => o.UserId == userId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync();

        diagnostics.AppendLine($"   - Tổng số đơn hàng: {await _context.Orders.CountAsync(o => o.UserId == userId.Value)}");
        diagnostics.AppendLine($"   - 5 đơn hàng gần nhất:");
        
        if (orders.Any())
        {
            foreach (var order in orders)
            {
                diagnostics.AppendLine($"     * Order #{order.OrderNumber} - {order.Status} - {order.CreatedAt:yyyy-MM-dd HH:mm}");
            }
        }
        else
        {
            diagnostics.AppendLine("     * Không có đơn hàng nào");
        }
        diagnostics.AppendLine();

        // 5. Kiểm tra tất cả users và orders
        diagnostics.AppendLine("5. TỔNG QUAN HỆ THỐNG:");
        var totalUsers = await _context.Users.CountAsync();
        var totalOrders = await _context.Orders.CountAsync();
        var usersWithOrders = await _context.Orders
            .Select(o => o.UserId)
            .Distinct()
            .CountAsync();

        diagnostics.AppendLine($"   - Tổng số users: {totalUsers}");
        diagnostics.AppendLine($"   - Tổng số orders: {totalOrders}");
        diagnostics.AppendLine($"   - Users có đơn hàng: {usersWithOrders}");
        diagnostics.AppendLine();

        // 6. Hướng dẫn sửa lỗi
        diagnostics.AppendLine("6. HƯỚNG DẪN SỬA LỖI:");
        
        if (!migrationStatus.IsCompleted)
        {
            diagnostics.AppendLine("   ⚠️ RBAC Migration chưa hoàn thành!");
            diagnostics.AppendLine("   → Truy cập: /Admin/Role/MigrationStatus");
            diagnostics.AppendLine("   → Nhấn nút 'Run Migration'");
            diagnostics.AppendLine("   → Đăng xuất và đăng nhập lại");
        }
        else if (!hasManageRbac && currentUser?.Role == Models.UserRole.Admin)
        {
            diagnostics.AppendLine("   ⚠️ Admin user không có quyền 'system.manage_rbac'!");
            diagnostics.AppendLine("   → Có thể cần chạy lại migration");
            diagnostics.AppendLine("   → Hoặc gán quyền thủ công trong database");
        }
        else if (hasManageRbac)
        {
            diagnostics.AppendLine("   ✓ RBAC đã được cấu hình đúng!");
            diagnostics.AppendLine("   → Bạn có thể truy cập /Admin/Role");
        }

        if (orders.Count == 0 && currentUser?.Role == Models.UserRole.Customer)
        {
            diagnostics.AppendLine("   ⚠️ User không có đơn hàng nào!");
            diagnostics.AppendLine("   → Tạo đơn hàng mới để test");
        }

        return Content(diagnostics.ToString(), "text/plain");
    }

    /// <summary>
    /// Chạy migration RBAC nhanh - Không cần phân quyền
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RunMigration()
    {
        try
        {
            var result = await _migrationService.MigrateToRbacAsync();
            
            if (result.Success)
            {
                return Content($"✓ Migration thành công!\n" +
                             $"- Users processed: {result.UsersProcessed}\n" +
                             $"- Completed at: {result.CompletedAt}\n\n" +
                             $"Vui lòng đăng xuất và đăng nhập lại.", "text/plain");
            }
            else
            {
                return Content($"✗ Migration thất bại!\n" +
                             $"Errors:\n{string.Join("\n", result.Errors)}", "text/plain");
            }
        }
        catch (Exception ex)
        {
            return Content($"✗ Lỗi: {ex.Message}\n{ex.StackTrace}", "text/plain");
        }
    }

    /// <summary>
    /// Trang migration với form để chạy migration - Không cần phân quyền
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Migration()
    {
        var status = await _migrationService.GetMigrationStatusAsync();
        ViewBag.Status = status;
        return View();
    }

    /// <summary>
    /// Migrate existing addresses from 3-level to 2-level via AddressKit API.
    /// Run BEFORE applying the SwitchToTwoLevelAddress EF migration.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> MigrateAddresses()
    {
        try
        {
            var result = await _migrationService.MigrateAddressesToTwoLevelAsync();

            if (result.Success)
            {
                return Content($"✓ Address migration successful!\n" +
                             $"- Addresses processed: {result.UsersProcessed}\n" +
                             $"- Completed at: {result.CompletedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                             $"You can now apply the EF migration:\n" +
                             $"  dotnet ef database update", "text/plain");
            }
            else
            {
                var msg = $"✗ Address migration completed with errors!\n" +
                          $"- Processed: {result.UsersProcessed}\n" +
                          $"Errors:\n{string.Join("\n", result.Errors.Take(20))}";
                if (result.Errors.Count > 20)
                    msg += $"\n... and {result.Errors.Count - 20} more errors";
                return Content(msg, "text/plain");
            }
        }
        catch (Exception ex)
        {
            return Content($"✗ Fatal error: {ex.Message}\n{ex.StackTrace}", "text/plain");
        }
    }

    /// <summary>
    /// Protected read-only status for the aggregate JSON backfill.
    /// The CLI remains the only path that applies the backfill.
    /// </summary>
    [HttpGet]
    [RequirePermission("system.manage_rbac")]
    public async Task<IActionResult> DatabaseConsolidationStatus()
    {
        if (_databaseConsolidationService is null)
            return Problem("Database consolidation service is not registered.");

        var report = await _databaseConsolidationService.VerifyAsync(HttpContext.RequestAborted);
        var status = new System.Text.StringBuilder();
        status.AppendLine($"Success: {report.Success}");
        status.AppendLine($"ISJSON: {report.IsJsonValid}");
        status.AppendLine("Source counts:");
        foreach (var count in report.SourceCounts.OrderBy(item => item.Key))
            status.AppendLine($"- {count.Key}: {count.Value}");
        status.AppendLine("Target counts:");
        foreach (var count in report.TargetCounts.OrderBy(item => item.Key))
            status.AppendLine($"- {count.Key}: {count.Value}");
        status.AppendLine("Errors:");
        foreach (var error in report.Errors)
            status.AppendLine($"- {error.AggregateType} {error.SourceId}: {error.Message}");
        return Content(status.ToString(), "text/plain");
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}
