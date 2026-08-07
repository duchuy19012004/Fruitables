using Fruitables.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Fruitables.Tests;

/// <summary>
/// Phase 3: Role Management — CRUD, toggle, queries, cache invalidation.
/// NOTE: ApplicationDbContext seeds User Id=1 (Admin) and Id=2 (SuperAdmin).
///       All test-specific users use Id >= 1001.
/// </summary>
public class RbacRoleManagementTests
{
    // --- CreateRoleAsync -----------------------------------------------------

    [Fact]
    public async Task CreateRoleAsync_Success_CreatesActiveRoleAndAudit()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        await ctx.SaveChangesAsync();

        var svc  = RbacTestHelper.CreateService(ctx);
        var role = await svc.CreateRoleAsync("Manager", "Manages stuff", adminId: 1001);

        var dbRole = await ctx.Roles.FindAsync(role.Id);
        Assert.NotNull(dbRole);
        Assert.Equal("Manager", dbRole!.Name);
        Assert.True(dbRole.IsActive);

        var audit = await ctx.RbacAuditLogs
            .FirstOrDefaultAsync(a => a.Action == "Create" && a.EntityType == "Role");
        Assert.NotNull(audit);
        Assert.Contains("Manager", audit!.NewValue);
    }

    [Fact]
    public async Task CreateRoleAsync_EmptyName_ThrowsArgumentException()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CreateRoleAsync("   ", null, adminId: 1001));
    }

    [Fact]
    public async Task CreateRoleAsync_DuplicateName_ThrowsInvalidOperationException()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateRoleAsync("Editor", null, adminId: 1001));
    }

    // --- UpdateRoleAsync -----------------------------------------------------

    [Fact]
    public async Task UpdateRoleAsync_Success_UpdatesFieldsAndAudit()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedActiveRole(ctx, 10, "OldName", "OldDesc");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var updated = await svc.UpdateRoleAsync(10, "NewName", "NewDesc", adminId: 1001);

        Assert.Equal("NewName",  updated.Name);
        Assert.Equal("NewDesc",  updated.Description);

        var audit = await ctx.RbacAuditLogs
            .FirstOrDefaultAsync(a => a.Action == "Update" && a.EntityType == "Role");
        Assert.NotNull(audit);
        Assert.Contains("OldName", audit!.OldValue);
        Assert.Contains("NewName", audit!.NewValue);
    }

    [Fact]
    public async Task UpdateRoleAsync_NotFound_Throws()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateRoleAsync(999, "X", null, adminId: 1001));
    }

    [Fact]
    public async Task UpdateRoleAsync_EmptyName_ThrowsArgumentException()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedActiveRole(ctx, 10, "SomeRole");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.UpdateRoleAsync(10, "", null, adminId: 1001));
    }

    [Fact]
    public async Task UpdateRoleAsync_DuplicateNameExcludingCurrentRole_Throws()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedActiveRole(ctx, 10, "RoleA");
        RbacTestHelper.SeedActiveRole(ctx, 11, "RoleB");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateRoleAsync(10, "RoleB", null, adminId: 1001));
    }

    [Fact]
    public async Task UpdateRoleAsync_InvalidatesCacheForUsersWithRole()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        RbacTestHelper.SeedPermission(ctx, 1, "products.view", "products");
        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc   = RbacTestHelper.CreateService(ctx, cache);

        // Populate cache
        var before = await svc.GetUserPermissionsAsync(1100);
        Assert.Contains("products.view", before);

        // Remove permission mapping directly, then update role (triggers cache invalidation)
        ctx.RolePermissions.RemoveRange(ctx.RolePermissions);
        await ctx.SaveChangesAsync();
        await svc.UpdateRoleAsync(10, "Editor", null, adminId: 1001);

        // Cache was invalidated — re-read from DB returns empty
        var after = await svc.GetUserPermissionsAsync(1100);
        Assert.Empty(after);
    }

    // --- DeleteRoleAsync -----------------------------------------------------

    [Fact]
    public async Task DeleteRoleAsync_UnassignedRole_DeletesAndAudits()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedActiveRole(ctx, 10, "TempRole");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        await svc.DeleteRoleAsync(10, adminId: 1001);

        Assert.Null(await ctx.Roles.FindAsync(10));

        var audit = await ctx.RbacAuditLogs
            .FirstOrDefaultAsync(a => a.Action == "Delete" && a.EntityType == "Role");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task DeleteRoleAsync_NotFound_Throws()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeleteRoleAsync(999, adminId: 1001));
    }

    [Fact]
    public async Task DeleteRoleAsync_AssignedRole_ThrowsAndDoesNotDelete()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        RbacTestHelper.SeedActiveRole(ctx, 10, "InUse");
        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeleteRoleAsync(10, adminId: 1001));

        Assert.NotNull(await ctx.Roles.FindAsync(10));
    }

    // --- ToggleRoleActiveAsync -----------------------------------------------

    [Fact]
    public async Task ToggleRoleActiveAsync_Deactivate_SetsInactiveAndAuditsDeactivate()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        await ctx.SaveChangesAsync();

        var svc    = RbacTestHelper.CreateService(ctx);
        var result = await svc.ToggleRoleActiveAsync(10, isActive: false, adminId: 1001);

        Assert.False(result.IsActive);

        var audit = await ctx.RbacAuditLogs
            .FirstOrDefaultAsync(a => a.Action == "Deactivate");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task ToggleRoleActiveAsync_Activate_SetsActiveAndAuditsActivate()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedInactiveRole(ctx, 10, "Disabled");
        await ctx.SaveChangesAsync();

        var svc    = RbacTestHelper.CreateService(ctx);
        var result = await svc.ToggleRoleActiveAsync(10, isActive: true, adminId: 1001);

        Assert.True(result.IsActive);

        var audit = await ctx.RbacAuditLogs
            .FirstOrDefaultAsync(a => a.Action == "Activate");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task ToggleRoleActiveAsync_NotFound_Throws()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ToggleRoleActiveAsync(999, false, adminId: 1001));
    }

    // --- GetRoleByIdAsync ----------------------------------------------------

    [Fact]
    public async Task GetRoleByIdAsync_Found_ReturnsRole()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var role = await svc.GetRoleByIdAsync(10);

        Assert.NotNull(role);
        Assert.Equal("Editor", role!.Name);
    }

    [Fact]
    public async Task GetRoleByIdAsync_NotFound_ReturnsNull()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var role = await svc.GetRoleByIdAsync(999);

        Assert.Null(role);
    }

    // --- GetAllRolesAsync ----------------------------------------------------

    [Fact]
    public async Task GetAllRolesAsync_ExcludeInactive_ReturnsOnlyActive()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedActiveRole(ctx, 1, "Active");
        RbacTestHelper.SeedInactiveRole(ctx, 2, "Inactive");
        await ctx.SaveChangesAsync();

        var svc   = RbacTestHelper.CreateService(ctx);
        var roles = await svc.GetAllRolesAsync(includeInactive: false);

        Assert.Single(roles);
        Assert.Equal("Active", roles[0].Name);
    }

    [Fact]
    public async Task GetAllRolesAsync_IncludeInactive_ReturnsAll()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedActiveRole(ctx, 1, "Active");
        RbacTestHelper.SeedInactiveRole(ctx, 2, "Inactive");
        await ctx.SaveChangesAsync();

        var svc   = RbacTestHelper.CreateService(ctx);
        var roles = await svc.GetAllRolesAsync(includeInactive: true);

        Assert.Equal(2, roles.Count);
    }

    //GetRolesPagedAsync 

    [Fact]
    public async Task GetRolesPagedAsync_PaginatesCorrectly()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        for (int i = 1; i <= 5; i++)
            RbacTestHelper.SeedActiveRole(ctx, i, $"Role{i:D2}");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var (roles, total) = await svc.GetRolesPagedAsync(page: 2, pageSize: 2);

        Assert.Equal(5, total);
        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task GetRolesPagedAsync_SearchFiltersNameAndDescription()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedActiveRole(ctx, 1, "Administrator", "Manages everything");
        RbacTestHelper.SeedActiveRole(ctx, 2, "Viewer",        "Read-only access");
        RbacTestHelper.SeedActiveRole(ctx, 3, "Editor",        "Can edit content");
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var (roles, total) = await svc.GetRolesPagedAsync(1, 10, searchTerm: "edit");

        Assert.Equal(1, total);
        Assert.Single(roles);
        Assert.Equal("Editor", roles[0].Name);
    }

    // --- InvalidateRoleCacheAsync --------------------------------------------

    [Fact]
    public async Task InvalidateRoleCacheAsync_InvalidatesOnlyUsersWithTargetRole()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100); // User A
        RbacTestHelper.SeedCustomerUser(ctx, 1101); // User B

        RbacTestHelper.SeedActiveRole(ctx, 10, "RoleA");
        RbacTestHelper.SeedActiveRole(ctx, 20, "RoleB");

        RbacTestHelper.SeedPermission(ctx, 1, "perm.a", "a");
        RbacTestHelper.SeedPermission(ctx, 2, "perm.b", "b");

        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedRolePermission(ctx, 2, 20, 2, assignedByAdminId: 1001);

        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 2, 1101, 20, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = RbacTestHelper.CreateService(ctx, cache);

        // Populate cache for both
        var permsA_Before = await svc.GetUserPermissionsAsync(1100);
        var permsB_Before = await svc.GetUserPermissionsAsync(1101);
        Assert.Contains("perm.a", permsA_Before);
        Assert.Contains("perm.b", permsB_Before);

        // Remove perm A from Role A directly in DB
        var rpA = await ctx.RolePermissions.FirstAsync(rp => rp.RoleId == 10);
        ctx.RolePermissions.Remove(rpA);

        // Remove perm B from Role B directly in DB to verify cache isolation
        var rpB = await ctx.RolePermissions.FirstAsync(rp => rp.RoleId == 20);
        ctx.RolePermissions.Remove(rpB);

        await ctx.SaveChangesAsync();

        // Invalidate Role A
        await svc.InvalidateRoleCacheAsync(10);

        // Re-read: User A should hit DB and get empty. User B should hit cache and still have perm.b
        var permsA_After = await svc.GetUserPermissionsAsync(1100);
        var permsB_After = await svc.GetUserPermissionsAsync(1101);

        Assert.Empty(permsA_After);
        Assert.Contains("perm.b", permsB_After);
    }
}
