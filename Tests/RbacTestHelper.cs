using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Fruitables.Services.Identity.Rbac;

namespace Fruitables.Tests;

/// <summary>
/// Shared seed/factory helpers for RBAC test classes.
/// 
/// IMPORTANT: ApplicationDbContext seeds User Id=1 (Admin) and Id=2 (SuperAdmin) via HasData().
/// All test user IDs must be >= 1000 to avoid UNIQUE constraint failures on SQLite.
/// </summary>
public static class RbacTestHelper
{
    // ── Service factory ──────────────────────────────────────────────────────

    /// <summary>Creates a <see cref="RbacService"/> with a real <see cref="MemoryCache"/>.</summary>
    public static RbacService CreateService(ApplicationDbContext context, IMemoryCache? cache = null)
    {
        cache ??= new MemoryCache(new MemoryCacheOptions());
        return new RbacService(
            new UnitOfWork(context),
            cache,
            NullLogger<RbacService>.Instance);
    }

    // ── Context factories ────────────────────────────────────────────────────

    /// <summary>SQLite in-memory context (FK + transaction support).</summary>
    public static ApplicationDbContext CreateSqliteContext()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        return new ApplicationDbContext(options);
    }

    // ── Seed helpers ─────────────────────────────────────────────────────────
    // NOTE: Use IDs >= 1000 to avoid conflicts with seeded users (Id=1, Id=2).

    /// <summary>Seeds an admin-role user. Default id=1001.</summary>
    public static User SeedAdminUser(ApplicationDbContext ctx, int id = 1001)
    {
        var user = new User
        {
            Id = id,
            Name = $"Admin User {id}",
            Email = $"admin{id}@test.com",
            Password = "hashed",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(user);
        return user;
    }

    /// <summary>Seeds a customer-role user. Default id=1100.</summary>
    public static User SeedCustomerUser(ApplicationDbContext ctx, int id = 1100)
    {
        var user = new User
        {
            Id = id,
            Name = $"Customer User {id}",
            Email = $"customer{id}@test.com",
            Password = "hashed",
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(user);
        return user;
    }

    public static Role SeedActiveRole(ApplicationDbContext ctx, int id, string name, string? description = null)
    {
        var role = new Role
        {
            Id = id,
            Name = name,
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Roles.Add(role);
        return role;
    }

    public static Role SeedInactiveRole(ApplicationDbContext ctx, int id, string name)
    {
        var role = new Role
        {
            Id = id,
            Name = name,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Roles.Add(role);
        return role;
    }

    public static Permission SeedPermission(ApplicationDbContext ctx, int id, string name, string module)
    {
        var permission = new Permission
        {
            Id = id,
            Name = name,
            Module = module,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Permissions.Add(permission);
        return permission;
    }

    public static UserRoleMapping SeedUserRoleMapping(
        ApplicationDbContext ctx, int id, int userId, int roleId, int assignedByAdminId = 1)
    {
        var mapping = new UserRoleMapping
        {
            Id = id,
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedByAdminId = assignedByAdminId
        };
        ctx.UserRoleMappings.Add(mapping);
        return mapping;
    }

    public static RolePermission SeedRolePermission(
        ApplicationDbContext ctx, int id, int roleId, int permissionId, int assignedByAdminId = 1)
    {
        var rp = new RolePermission
        {
            Id = id,
            RoleId = roleId,
            PermissionId = permissionId,
            AssignedAt = DateTime.UtcNow,
            AssignedByAdminId = assignedByAdminId
        };
        ctx.RolePermissions.Add(rp);
        return rp;
    }
}
