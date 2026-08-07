using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Identity.Rbac;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fruitables.Tests;

/// <summary>
/// Shared seed/factory helpers for RBAC test classes.
/// User IDs must be >= 1000 to avoid UNIQUE conflicts with HasData seeds.
/// </summary>
public static class RbacTestHelper
{
    private static readonly VersionedJsonSerializer Serializer = new();

    public static RbacService CreateService(ApplicationDbContext context, IMemoryCache? cache = null)
    {
        cache ??= new MemoryCache(new MemoryCacheOptions());
        return new RbacService(
            new UnitOfWork(context),
            cache,
            NullLogger<RbacService>.Instance,
            Serializer);
    }

    public static ApplicationDbContext CreateSqliteContext()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        return new ApplicationDbContext(options);
    }

    public static User SeedAdminUser(ApplicationDbContext ctx, int id = 1001)
    {
        var user = new User
        {
            Id = id,
            Name = $"Admin User {id}",
            Email = $"admin{id}@test.com",
            Password = "hashed",
            Role = UserRole.Admin,
            RoleIdsJson = Serializer.Serialize(new UserRolesDocument { UserId = id, Roles = [] }),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(user);
        return user;
    }

    public static User SeedCustomerUser(ApplicationDbContext ctx, int id = 1100)
    {
        var user = new User
        {
            Id = id,
            Name = $"Customer User {id}",
            Email = $"customer{id}@test.com",
            Password = "hashed",
            Role = UserRole.Customer,
            RoleIdsJson = Serializer.Serialize(new UserRolesDocument { UserId = id, Roles = [] }),
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
            PermissionsJson = Serializer.Serialize(new RolePermissionsDocument { RoleId = id, Permissions = [] }),
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
            PermissionsJson = Serializer.Serialize(new RolePermissionsDocument { RoleId = id, Permissions = [] }),
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
        var role = ctx.Roles.Local.FirstOrDefault(item => item.Id == roleId)
            ?? ctx.Roles.FirstOrDefault(item => item.Id == roleId);
        var user = ctx.Users.Local.FirstOrDefault(item => item.Id == userId)
            ?? ctx.Users.FirstOrDefault(item => item.Id == userId);

        if (user != null)
        {
            var document = string.IsNullOrWhiteSpace(user.RoleIdsJson) || user.RoleIdsJson.Trim() == "[]"
                ? new UserRolesDocument { UserId = userId }
                : Serializer.Deserialize<UserRolesDocument>(user.RoleIdsJson);
            if (document.Roles.All(item => item.RoleId != roleId))
            {
                user.RoleIdsJson = Serializer.Serialize(new UserRolesDocument
                {
                    UserId = userId,
                    Roles =
                    [
                        ..document.Roles,
                        new UserRoleEntry
                        {
                            RoleId = roleId,
                            RoleName = role?.Name ?? $"role-{roleId}",
                            AssignedAt = DateTime.UtcNow,
                            AssignedByAdminId = assignedByAdminId
                        }
                    ]
                });
            }
        }

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
        var role = ctx.Roles.Local.FirstOrDefault(item => item.Id == roleId)
            ?? ctx.Roles.FirstOrDefault(item => item.Id == roleId);
        var permission = ctx.Permissions.Local.FirstOrDefault(item => item.Id == permissionId)
            ?? ctx.Permissions.FirstOrDefault(item => item.Id == permissionId);

        if (role != null)
        {
            var document = string.IsNullOrWhiteSpace(role.PermissionsJson) || role.PermissionsJson.Trim() == "[]"
                ? new RolePermissionsDocument { RoleId = roleId }
                : Serializer.Deserialize<RolePermissionsDocument>(role.PermissionsJson);
            if (document.Permissions.All(item => item.PermissionId != permissionId))
            {
                role.PermissionsJson = Serializer.Serialize(new RolePermissionsDocument
                {
                    RoleId = roleId,
                    Permissions =
                    [
                        ..document.Permissions,
                        new RolePermissionEntry
                        {
                            PermissionId = permissionId,
                            PermissionName = permission?.Name ?? $"permission-{permissionId}",
                            AssignedAt = DateTime.UtcNow,
                            AssignedByAdminId = assignedByAdminId
                        }
                    ]
                });
            }
        }

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
