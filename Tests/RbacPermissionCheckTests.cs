using Fruitables.Models;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Fruitables.Tests;

/// <summary>
/// Phase 2: Permission Check and Cache
/// NOTE: ApplicationDbContext seeds User Id=1 (Admin) and Id=2 (SuperAdmin).
///       All test-specific users use Id >= 1001.
/// </summary>
public class RbacPermissionCheckTests
{
    // --- GetUserPermissionsAsync ---------------------------------------------

    [Fact]
    public async Task GetUserPermissionsAsync_ReturnsDistinctPermissionsFromActiveRoles()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        // User 1001 = admin (assigns), user 1100 = subject under test
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);

        var role1 = RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        var role2 = RbacTestHelper.SeedActiveRole(ctx, 11, "Viewer");

        RbacTestHelper.SeedPermission(ctx, 1, "products.view",   "products");
        RbacTestHelper.SeedPermission(ctx, 2, "products.create", "products");

        // Both roles share products.view — result must be distinct
        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedRolePermission(ctx, 2, 10, 2, assignedByAdminId: 1001);
        RbacTestHelper.SeedRolePermission(ctx, 3, 11, 1, assignedByAdminId: 1001); // duplicate products.view

        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 2, 1100, 11, assignedByAdminId: 1001);

        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var permissions = await svc.GetUserPermissionsAsync(1100);

        Assert.Contains("products.view",   permissions);
        Assert.Contains("products.create", permissions);
        Assert.Equal(permissions.Count, permissions.Distinct().Count()); // no duplicates
    }

    [Fact]
    public async Task GetUserPermissionsAsync_InactiveRoleDoesNotContributePermissions()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);

        RbacTestHelper.SeedInactiveRole(ctx, 10, "InactiveRole");
        RbacTestHelper.SeedPermission(ctx, 1, "orders.view", "orders");

        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var permissions = await svc.GetUserPermissionsAsync(1100);

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_UserWithNoRoles_ReturnsEmpty()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);
        var permissions = await svc.GetUserPermissionsAsync(1100);

        Assert.Empty(permissions);
    }

    // --- HasPermissionAsync --------------------------------------------------

    [Fact]
    public async Task HasPermissionAsync_ReturnsTrueWhenUserHasPermission()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        RbacTestHelper.SeedPermission(ctx, 1, "products.view", "products");
        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.True(await svc.HasPermissionAsync(1100, "products.view"));
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalseWhenUserLacksPermission()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.False(await svc.HasPermissionAsync(1100, "products.create"));
    }

    // --- HasAnyPermissionAsync -----------------------------------------------

    [Fact]
    public async Task HasAnyPermissionAsync_EmptyParams_ReturnsFalse()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.False(await svc.HasAnyPermissionAsync(1100));          // empty params
    }

    [Fact]
    public async Task HasAnyPermissionAsync_NullList_ReturnsFalse()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.False(await svc.HasAnyPermissionAsync(1100, null!));   // null list
    }

    [Fact]
    public async Task HasAnyPermissionAsync_TrueWhenAtLeastOneMatches()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        RbacTestHelper.SeedPermission(ctx, 1, "products.view", "products");
        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.True(await svc.HasAnyPermissionAsync(1100, "products.view", "orders.delete"));
    }

    [Fact]
    public async Task HasAnyPermissionAsync_FalseWhenNoneMatch()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.False(await svc.HasAnyPermissionAsync(1100, "orders.delete", "users.ban"));
    }

    // --- HasAllPermissionsAsync ----------------------------------------------

    [Fact]
    public async Task HasAllPermissionsAsync_EmptyParams_ReturnsFalse()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.False(await svc.HasAllPermissionsAsync(1100));         // empty params
    }

    [Fact]
    public async Task HasAllPermissionsAsync_NullList_ReturnsFalse()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.False(await svc.HasAllPermissionsAsync(1100, null!));  // null list
    }

    [Fact]
    public async Task HasAllPermissionsAsync_TrueWhenAllMatch()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        RbacTestHelper.SeedPermission(ctx, 1, "products.view",   "products");
        RbacTestHelper.SeedPermission(ctx, 2, "products.create", "products");
        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedRolePermission(ctx, 2, 10, 2, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.True(await svc.HasAllPermissionsAsync(1100, "products.view", "products.create"));
    }

    [Fact]
    public async Task HasAllPermissionsAsync_FalseWhenOneMissing()
    {
        using var ctx = RbacTestHelper.CreateSqliteContext();
        RbacTestHelper.SeedAdminUser(ctx, 1001);
        RbacTestHelper.SeedCustomerUser(ctx, 1100);
        RbacTestHelper.SeedActiveRole(ctx, 10, "Editor");
        RbacTestHelper.SeedPermission(ctx, 1, "products.view", "products");
        RbacTestHelper.SeedRolePermission(ctx, 1, 10, 1, assignedByAdminId: 1001);
        RbacTestHelper.SeedUserRoleMapping(ctx, 1, 1100, 10, assignedByAdminId: 1001);
        await ctx.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(ctx);

        Assert.False(await svc.HasAllPermissionsAsync(1100, "products.view", "products.create"));
    }

    // --- Cache behavior ------------------------------------------------------

    [Fact]
    public async Task GetUserPermissionsAsync_CachesOnFirstCall_AndReturnsCachedOnSecond()
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

        // First call — DB read, result cached
        var first = await svc.GetUserPermissionsAsync(1100);
        Assert.Contains("products.view", first);

        // Directly remove permission from DB without invalidating cache
        ctx.RolePermissions.RemoveRange(ctx.RolePermissions);
        await ctx.SaveChangesAsync();

        // Second call — should still return cached value
        var second = await svc.GetUserPermissionsAsync(1100);
        Assert.Contains("products.view", second);
    }

    [Fact]
    public async Task InvalidateUserCacheAsync_ForcesReReadFromDb()
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
        await svc.GetUserPermissionsAsync(1100);

        // Modify DB
        ctx.RolePermissions.RemoveRange(ctx.RolePermissions);
        await ctx.SaveChangesAsync();

        // Invalidate, then re-read
        await svc.InvalidateUserCacheAsync(1100);
        var afterInvalidate = await svc.GetUserPermissionsAsync(1100);

        Assert.Empty(afterInvalidate);
    }
}
