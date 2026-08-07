using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Auditing;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fruitables.Services.Identity.Rbac;

/// <summary>
/// Implementation of RBAC (Role-Based Access Control) Service
/// Provides functionality for managing roles, permissions, and authorization
/// </summary>
public class RbacService : IRbacService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RbacService> _logger;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly IAuditLogWriter _auditLogWriter;
    private const string CacheKeyPrefix = "rbac:user:";
    private const string CacheKeySuffix = ":permissions";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public RbacService(
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        ILogger<RbacService> logger,
        IJsonDocumentSerializer? serializer = null,
        IAuditLogWriter? auditLogWriter = null)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _auditLogWriter = auditLogWriter ?? new AuditLogWriter(((Repositories.UnitOfWork)unitOfWork).Context);
    }

    // ==================== Helper Methods ====================
    
    private string GetUserCacheKey(int userId) => $"{CacheKeyPrefix}{userId}{CacheKeySuffix}";
    
    private async Task CreateAuditLogAsync(
        string action,
        string entityType,
        int entityId,
        int adminId,
        string? oldValue = null,
        string? newValue = null)
    {
        await _auditLogWriter.WriteAsync(action, entityType, entityId, adminId, oldValue, newValue);

        // Expand-phase mirror so existing admin screens/tests still see RbacAuditLogs.
        await _unitOfWork.RbacAuditLogs.AddAsync(new RbacAuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ChangedByAdminId = adminId,
            ChangedAt = DateTime.UtcNow,
            OldValue = oldValue,
            NewValue = newValue
        });
        await _unitOfWork.SaveChangesAsync();
    }

    // ==================== Kiểm tra quyền hạn ====================
    
    public async Task<bool> HasPermissionAsync(int userId, string permissionName)
    {
        try
        {
            var permissions = await GetUserPermissionsAsync(userId);
            return permissions.Contains(permissionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {PermissionName} for user {UserId}", permissionName, userId);
            return false;
        }
    }
    
    public async Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionNames)
    {
        if (permissionNames == null || permissionNames.Length == 0)
            return false;
            
        try
        {
            var userPermissions = await GetUserPermissionsAsync(userId);
            return permissionNames.Any(p => userPermissions.Contains(p));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking any permissions for user {UserId}", userId);
            return false;
        }
    }
    
    public async Task<bool> HasAllPermissionsAsync(int userId, params string[] permissionNames)
    {
        if (permissionNames == null || permissionNames.Length == 0)
            return false;
            
        try
        {
            var userPermissions = await GetUserPermissionsAsync(userId);
            return permissionNames.All(p => userPermissions.Contains(p));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking all permissions for user {UserId}", userId);
            return false;
        }
    }
    
    public async Task<List<string>> GetUserPermissionsAsync(int userId)
    {
        // Try to get from cache first
        var cacheKey = GetUserCacheKey(userId);
        if (_cache.TryGetValue(cacheKey, out List<string>? cachedPermissions) && cachedPermissions != null)
        {
            _logger.LogDebug("Cache hit for user {UserId} permissions", userId);
            return cachedPermissions;
        }
        
        _logger.LogDebug("Cache miss for user {UserId} permissions, querying database", userId);

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return new List<string>();

        var roleDocument = RbacAggregateJson.ReadUserRoles(user.RoleIdsJson, _serializer);
        var roleIds = roleDocument.Roles.Select(role => role.RoleId).Distinct().ToList();
        if (roleIds.Count == 0)
        {
            _logger.LogDebug("User {UserId} has no active roles", userId);
            return new List<string>();
        }

        var roles = await _unitOfWork.Roles.Query()
            .Where(role => roleIds.Contains(role.Id) && role.IsActive)
            .ToListAsync();

        var permissions = roles
            .SelectMany(role => RbacAggregateJson.ReadRolePermissions(role.PermissionsJson, _serializer).Permissions)
            .Select(permission => permission.PermissionName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _cache.Set(cacheKey, permissions, CacheDuration);
        _logger.LogDebug("Cached {Count} permissions for user {UserId}", permissions.Count, userId);

        return permissions;
    }

    // ==================== Quản lý vai trò ====================
    
    public async Task<Role> CreateRoleAsync(string name, string? description, int adminId)
    {
        // Validate name
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Attempt to create role with empty name by admin {AdminId}", adminId);
            throw new ArgumentException("Role name is required", nameof(name));
        }
        
        // Check for duplicate name
        var existingRole = await _unitOfWork.Roles
            .Query()
            .FirstOrDefaultAsync(r => r.Name == name);
            
        if (existingRole != null)
        {
            _logger.LogWarning("Attempt to create duplicate role {RoleName} by admin {AdminId}", name, adminId);
            throw new InvalidOperationException("Role name already exists");
        }
        
        var role = new Role
        {
            Name = name,
            Description = description,
            IsActive = true,
            PermissionsJson = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _unitOfWork.Roles.AddAsync(role);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            "Create",
            "Role",
            role.Id,
            adminId,
            null,
            JsonSerializer.Serialize(new { role.Name, role.Description, role.IsActive })
        );
        await _unitOfWork.SaveChangesAsync();
        
        _logger.LogInformation("Role {RoleName} created by admin {AdminId}", name, adminId);
        return role;
    }
    
    public async Task<Role> UpdateRoleAsync(int roleId, string name, string? description, int adminId)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
        if (role == null)
        {
            _logger.LogWarning("Attempt to update non-existent role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Role not found");
        }
        
        // Validate name
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Attempt to update role {RoleId} with empty name by admin {AdminId}", roleId, adminId);
            throw new ArgumentException("Role name is required", nameof(name));
        }
        
        // Check for duplicate name (excluding current role)
        var existingRole = await _unitOfWork.Roles
            .Query()
            .FirstOrDefaultAsync(r => r.Name == name && r.Id != roleId);
            
        if (existingRole != null)
        {
            _logger.LogWarning("Attempt to update role {RoleId} with duplicate name {RoleName} by admin {AdminId}", roleId, name, adminId);
            throw new InvalidOperationException("Role name already exists");
        }
        
        var oldValue = JsonSerializer.Serialize(new { role.Name, role.Description, role.IsActive });
        
        role.Name = name;
        role.Description = description;
        role.UpdatedAt = DateTime.UtcNow;
        
        _unitOfWork.Roles.Update(role);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            "Update",
            "Role",
            role.Id,
            adminId,
            oldValue,
            JsonSerializer.Serialize(new { role.Name, role.Description, role.IsActive })
        );
        await _unitOfWork.SaveChangesAsync();
        
        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId);
        
        _logger.LogInformation("Role {RoleId} updated by admin {AdminId}", roleId, adminId);
        return role;
    }
    
    public async Task DeleteRoleAsync(int roleId, int adminId)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
        if (role == null)
        {
            _logger.LogWarning("Attempt to delete non-existent role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Role not found");
        }
        
        // Check if role is assigned to any users via RoleIdsJson.
        var users = await _unitOfWork.Users.Query().AsNoTracking().ToListAsync();
        var hasUsers = users.Any(user =>
            RbacAggregateJson.ReadUserRoles(user.RoleIdsJson, _serializer).Roles.Any(role => role.RoleId == roleId));

        if (hasUsers)
        {
            _logger.LogWarning("Attempt to delete role {RoleId} that is assigned to users by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Cannot delete role that is assigned to users");
        }
        
        var oldValue = JsonSerializer.Serialize(new { role.Name, role.Description, role.IsActive });
        
        _unitOfWork.Roles.Remove(role);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            "Delete",
            "Role",
            roleId,
            adminId,
            oldValue,
            null
        );
        await _unitOfWork.SaveChangesAsync();
        
        _logger.LogInformation("Role {RoleId} deleted by admin {AdminId}", roleId, adminId);
    }
    
    public async Task<Role> ToggleRoleActiveAsync(int roleId, bool isActive, int adminId)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
        if (role == null)
        {
            _logger.LogWarning("Attempt to toggle non-existent role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Role not found");
        }
        
        var oldValue = JsonSerializer.Serialize(new { role.Name, role.Description, role.IsActive });
        
        role.IsActive = isActive;
        role.UpdatedAt = DateTime.UtcNow;
        
        _unitOfWork.Roles.Update(role);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            isActive ? "Activate" : "Deactivate",
            "Role",
            role.Id,
            adminId,
            oldValue,
            JsonSerializer.Serialize(new { role.Name, role.Description, role.IsActive })
        );
        await _unitOfWork.SaveChangesAsync();
        
        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId);
        
        _logger.LogInformation("Role {RoleId} {Action} by admin {AdminId}", roleId, isActive ? "activated" : "deactivated", adminId);
        return role;
    }
    
    public async Task<Role?> GetRoleByIdAsync(int roleId)
    {
        return await _unitOfWork.Roles.GetByIdAsync(roleId);
    }
    
    public async Task<List<Role>> GetAllRolesAsync(bool includeInactive = false)
    {
        var query = _unitOfWork.Roles.Query();
        
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        
        return await query.OrderBy(r => r.Name).ToListAsync();
    }
    
    public async Task<(List<Role> Roles, int TotalCount)> GetRolesPagedAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _unitOfWork.Roles.Query();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(r => r.Name.Contains(searchTerm) || 
                                    (r.Description != null && r.Description.Contains(searchTerm)));
        }
        
        var totalCount = await query.CountAsync();
        
        var roles = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (roles, totalCount);
    }

    // ==================== Quản lý quyền hạn ====================
    
    public async Task<Permission> CreatePermissionAsync(string name, string module, string? description, int adminId)
    {
        // Validate name format (module.action)
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Attempt to create permission with empty name by admin {AdminId}", adminId);
            throw new ArgumentException("Permission name is required", nameof(name));
        }
        
        if (!name.Contains('.') || name.Split('.').Length != 2 || 
            string.IsNullOrWhiteSpace(name.Split('.')[0]) || 
            string.IsNullOrWhiteSpace(name.Split('.')[1]))
        {
            _logger.LogWarning("Attempt to create permission with invalid format {PermissionName} by admin {AdminId}", name, adminId);
            throw new ArgumentException("Permission format must be module.action", nameof(name));
        }
        
        // Check for duplicate name
        var existingPermission = await _unitOfWork.Permissions
            .Query()
            .FirstOrDefaultAsync(p => p.Name == name);
            
        if (existingPermission != null)
        {
            _logger.LogWarning("Attempt to create duplicate permission {PermissionName} by admin {AdminId}", name, adminId);
            throw new InvalidOperationException("Permission name already exists");
        }
        
        var permission = new Permission
        {
            Name = name,
            Module = module,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        
        await _unitOfWork.Permissions.AddAsync(permission);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            "Create",
            "Permission",
            permission.Id,
            adminId,
            null,
            JsonSerializer.Serialize(new { permission.Name, permission.Module, permission.Description })
        );
        await _unitOfWork.SaveChangesAsync();
        
        _logger.LogInformation("Permission {PermissionName} created by admin {AdminId}", name, adminId);
        return permission;
    }
    
    public async Task<Permission?> GetPermissionByIdAsync(int permissionId)
    {
        return await _unitOfWork.Permissions.GetByIdAsync(permissionId);
    }
    
    public async Task<List<Permission>> GetAllPermissionsAsync()
    {
        return await _unitOfWork.Permissions
            .Query()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }
    
    public async Task<List<Permission>> GetPermissionsByModuleAsync(string module)
    {
        return await _unitOfWork.Permissions
            .Query()
            .Where(p => p.Module == module)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
    
    public async Task<Dictionary<string, List<Permission>>> GetPermissionsGroupedByModuleAsync()
    {
        var permissions = await GetAllPermissionsAsync();
        return permissions
            .GroupBy(p => p.Module)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
    
    public async Task DeletePermissionAsync(int permissionId, int adminId)
    {
        var permission = await _unitOfWork.Permissions.GetByIdAsync(permissionId);
        if (permission == null)
        {
            _logger.LogWarning("Attempt to delete non-existent permission {PermissionId} by admin {AdminId}", permissionId, adminId);
            throw new InvalidOperationException("Permission not found");
        }
        
        var roles = await _unitOfWork.Roles.Query().AsNoTracking().ToListAsync();
        var hasRoles = roles.Any(role =>
            RbacAggregateJson.ReadRolePermissions(role.PermissionsJson, _serializer)
                .Permissions.Any(item => item.PermissionId == permissionId));

        if (hasRoles)
        {
            _logger.LogWarning("Attempt to delete permission {PermissionId} that is assigned to roles by admin {AdminId}", permissionId, adminId);
            throw new InvalidOperationException("Cannot delete permission that is assigned to roles");
        }
        
        var oldValue = JsonSerializer.Serialize(new { permission.Name, permission.Module, permission.Description });
        
        _unitOfWork.Permissions.Remove(permission);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            "Delete",
            "Permission",
            permissionId,
            adminId,
            oldValue,
            null
        );
        await _unitOfWork.SaveChangesAsync();
        
        _logger.LogInformation("Permission {PermissionId} deleted by admin {AdminId}", permissionId, adminId);
    }

    // ==================== Gán quyền hạn cho vai trò ====================
    
    public async Task AssignPermissionToRoleAsync(int roleId, int permissionId, int adminId)
    {
        await AssignPermissionsToRoleAsync(roleId, [permissionId], adminId);
    }

    public async Task AssignPermissionsToRoleAsync(int roleId, List<int> permissionIds, int adminId)
    {
        if (permissionIds == null || permissionIds.Count == 0)
            return;

        var role = await _unitOfWork.Roles.GetByIdAsync(roleId)
            ?? throw new InvalidOperationException("Role not found");
        if (!role.IsActive)
            throw new InvalidOperationException("Cannot assign permission to inactive role");

        var uniquePermissionIds = permissionIds.Distinct().ToList();
        var permissions = await _unitOfWork.Permissions.Query()
            .Where(permission => uniquePermissionIds.Contains(permission.Id))
            .ToListAsync();
        if (permissions.Count != uniquePermissionIds.Count)
            throw new InvalidOperationException("One or more permissions not found");

        var document = RbacAggregateJson.ReadRolePermissions(role.PermissionsJson, _serializer);
        var existingIds = document.Permissions.Select(item => item.PermissionId).ToHashSet();
        var added = new List<RolePermissionEntry>();
        foreach (var permission in permissions)
        {
            if (existingIds.Contains(permission.Id))
                continue;
            added.Add(new RolePermissionEntry
            {
                PermissionId = permission.Id,
                PermissionName = permission.Name,
                AssignedAt = DateTime.UtcNow,
                AssignedByAdminId = adminId
            });
        }

        if (added.Count == 0)
            return;

        role.PermissionsJson = RbacAggregateJson.Serialize(
            RbacAggregateJson.WithPermissions(roleId, document.Permissions.Concat(added)),
            _serializer);
        role.RowVersion = Guid.NewGuid().ToByteArray();
        role.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Roles.Update(role);

        var newMappings = new List<RolePermission>();
        foreach (var entry in added)
        {
            var mapping = new RolePermission
            {
                RoleId = roleId,
                PermissionId = entry.PermissionId,
                AssignedAt = entry.AssignedAt,
                AssignedByAdminId = adminId
            };
            await _unitOfWork.RolePermissions.AddAsync(mapping);
            newMappings.Add(mapping);
        }

        await _unitOfWork.SaveChangesAsync();

        foreach (var mapping in newMappings)
        {
            var permissionName = added.First(item => item.PermissionId == mapping.PermissionId).PermissionName;
            await CreateAuditLogAsync("Assign", "RolePermission", mapping.Id, adminId, null,
                JsonSerializer.Serialize(new { RoleId = roleId, PermissionId = mapping.PermissionId, PermissionName = permissionName }));
        }

        await InvalidateRoleCacheAsync(roleId);
    }

    public async Task RevokePermissionFromRoleAsync(int roleId, int permissionId, int adminId)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
        if (role == null)
            return;

        var document = RbacAggregateJson.ReadRolePermissions(role.PermissionsJson, _serializer);
        var remaining = document.Permissions.Where(item => item.PermissionId != permissionId).ToList();
        var legacy = await _unitOfWork.RolePermissions.Query()
            .Include(item => item.Permission)
            .FirstOrDefaultAsync(item => item.RoleId == roleId && item.PermissionId == permissionId);
        if (remaining.Count == document.Permissions.Count && legacy == null)
            return;

        var removedName = document.Permissions.FirstOrDefault(item => item.PermissionId == permissionId)?.PermissionName
            ?? legacy?.Permission?.Name
            ?? "Unknown";
        role.PermissionsJson = RbacAggregateJson.Serialize(
            RbacAggregateJson.WithPermissions(roleId, remaining),
            _serializer);
        role.RowVersion = Guid.NewGuid().ToByteArray();
        role.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Roles.Update(role);
        if (legacy != null)
            _unitOfWork.RolePermissions.Remove(legacy);
        await _unitOfWork.SaveChangesAsync();

        await CreateAuditLogAsync("Revoke", "RolePermission", legacy?.Id ?? roleId, adminId,
            JsonSerializer.Serialize(new { RoleId = roleId, PermissionId = permissionId, PermissionName = removedName }),
            null);
        await InvalidateRoleCacheAsync(roleId);
    }

    public async Task<List<Permission>> GetRolePermissionsAsync(int roleId)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
        if (role == null)
            return [];

        var permissionIds = RbacAggregateJson.ReadRolePermissions(role.PermissionsJson, _serializer)
            .Permissions.Select(item => item.PermissionId).Distinct().ToList();
        if (permissionIds.Count == 0)
            return [];

        return await _unitOfWork.Permissions.Query()
            .Where(permission => permissionIds.Contains(permission.Id))
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Name)
            .ToListAsync();
    }

    // ==================== Gán vai trò cho người dùng ====================
    
    public async Task AssignRoleToUserAsync(int userId, int roleId, int adminId)
    {
        // Delegate to the atomic (transactional) multi-role path so that any failure inside the
        // role-assignment flow rolls back all mapping changes, audit logs, and the legacy role sync.
        await AssignRolesToUserAsync(userId, new List<int> { roleId }, adminId);
    }
    
    public async Task AssignRolesToUserAsync(int userId, List<int> roleIds, int adminId)
    {
        if (roleIds == null || !roleIds.Any())
            return;

        var uniqueRoleIds = roleIds.Distinct().ToList();
        if (uniqueRoleIds.Count > 1)
            throw new InvalidOperationException("Users can only have a single role assigned.");

        var user = await _unitOfWork.Users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found");

        var current = RbacAggregateJson.ReadUserRoles(user.RoleIdsJson, _serializer);
        var legacyRoleIds = await _unitOfWork.UserRoleMappings.Query()
            .Where(mapping => mapping.UserId == userId)
            .Select(mapping => mapping.RoleId)
            .ToListAsync();
        var existingRoleIds = current.Roles.Select(role => role.RoleId).Union(legacyRoleIds).Distinct().ToList();
        var allRoleIds = uniqueRoleIds.Union(existingRoleIds).Distinct().ToList();
        var roles = await _unitOfWork.Roles.Query().Where(role => allRoleIds.Contains(role.Id)).ToListAsync();
        var rolesDict = roles.ToDictionary(role => role.Id);

        foreach (var roleId in uniqueRoleIds)
        {
            if (!rolesDict.TryGetValue(roleId, out var role))
                throw new InvalidOperationException("One or more roles not found");
            if (!role.IsActive)
                throw new InvalidOperationException("Cannot assign inactive role");
        }

        var nextRoles = uniqueRoleIds.Select(roleId => new UserRoleEntry
        {
            RoleId = roleId,
            RoleName = rolesDict[roleId].Name,
            AssignedAt = DateTime.UtcNow,
            AssignedByAdminId = adminId
        }).ToList();

        if (existingRoleIds.SequenceEqual(uniqueRoleIds))
            return;

        user.RoleIdsJson = RbacAggregateJson.Serialize(
            RbacAggregateJson.WithRoles(userId, nextRoles),
            _serializer);
        user.RowVersion = Guid.NewGuid().ToByteArray();
        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Users.Update(user);

        var legacyMappings = await _unitOfWork.UserRoleMappings.Query()
            .Where(mapping => mapping.UserId == userId)
            .ToListAsync();
        var removeMappings = legacyMappings.Where(mapping => !uniqueRoleIds.Contains(mapping.RoleId)).ToList();
        if (removeMappings.Count > 0)
            _unitOfWork.UserRoleMappings.RemoveRange(removeMappings);

        var existingLegacyIds = legacyMappings.Select(mapping => mapping.RoleId).ToHashSet();
        foreach (var roleId in uniqueRoleIds.Where(id => !existingLegacyIds.Contains(id)))
        {
            await _unitOfWork.UserRoleMappings.AddAsync(new UserRoleMapping
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow,
                AssignedByAdminId = adminId
            });
        }

        await _unitOfWork.SaveChangesAsync();

        foreach (var removed in existingRoleIds.Except(uniqueRoleIds))
        {
            var roleName = rolesDict.TryGetValue(removed, out var role) ? role.Name : "Unknown";
            var mappingId = removeMappings.FirstOrDefault(item => item.RoleId == removed)?.Id ?? userId;
            await CreateAuditLogAsync("Revoke", "UserRole", mappingId, adminId,
                JsonSerializer.Serialize(new { UserId = userId, RoleId = removed, RoleName = roleName }), null);
        }

        foreach (var added in uniqueRoleIds.Except(existingRoleIds))
        {
            var mappingId = (await _unitOfWork.UserRoleMappings.Query()
                .FirstAsync(item => item.UserId == userId && item.RoleId == added)).Id;
            await CreateAuditLogAsync("Assign", "UserRole", mappingId, adminId, null,
                JsonSerializer.Serialize(new { UserId = userId, RoleId = added, RoleName = rolesDict[added].Name }));
        }

        await SyncUserLegacyRoleAsync(userId);
        await InvalidateUserCacheAsync(userId);
    }

    public async Task RevokeRoleFromUserAsync(int userId, int roleId, int adminId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return;

        var document = RbacAggregateJson.ReadUserRoles(user.RoleIdsJson, _serializer);
        var remaining = document.Roles.Where(role => role.RoleId != roleId).ToList();
        var legacy = await _unitOfWork.UserRoleMappings.Query()
            .FirstOrDefaultAsync(mapping => mapping.UserId == userId && mapping.RoleId == roleId);
        if (remaining.Count == document.Roles.Count && legacy == null)
            return;

        var removedName = document.Roles.FirstOrDefault(role => role.RoleId == roleId)?.RoleName
            ?? (await _unitOfWork.Roles.GetByIdAsync(roleId))?.Name
            ?? "Unknown";
        user.RoleIdsJson = RbacAggregateJson.Serialize(RbacAggregateJson.WithRoles(userId, remaining), _serializer);
        user.RowVersion = Guid.NewGuid().ToByteArray();
        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Users.Update(user);
        if (legacy != null)
            _unitOfWork.UserRoleMappings.Remove(legacy);
        await _unitOfWork.SaveChangesAsync();
        await SyncUserLegacyRoleAsync(userId);
        await CreateAuditLogAsync("Revoke", "UserRole", legacy?.Id ?? userId, adminId,
            JsonSerializer.Serialize(new { UserId = userId, RoleId = roleId, RoleName = removedName }), null);
        await InvalidateUserCacheAsync(userId);
    }

    public async Task<List<Role>> GetUserRolesAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return [];

        var roleIds = RbacAggregateJson.ReadUserRoles(user.RoleIdsJson, _serializer)
            .Roles.Select(role => role.RoleId).Distinct().ToList();
        if (roleIds.Count == 0)
            return [];

        return await _unitOfWork.Roles.Query()
            .Where(role => roleIds.Contains(role.Id))
            .OrderBy(role => role.Name)
            .ToListAsync();
    }
    
    /// <summary>
    /// Sync User.Role field with RBAC roles for backward compatibility
    /// Priority: SuperAdmin > Admin > Customer
    /// </summary>
    private async Task SyncUserLegacyRoleAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null) return;
        
        var roles = await GetUserRolesAsync(userId);
        var roleNames = roles.Select(r => r.Name).ToList();
        
        UserRole legacyRole;
        if (roleNames.Contains("SuperAdmin"))
            legacyRole = UserRole.SuperAdmin;
        else if (roleNames.Contains("Admin"))
            legacyRole = UserRole.Admin;
        else
            legacyRole = UserRole.Customer;
        
        if (user.Role != legacyRole)
        {
            user.Role = legacyRole;
            user.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogDebug("Synced legacy role for user {UserId} to {LegacyRole}", userId, legacyRole);
        }
    }

    // ==================== Quản lý cache ====================
    
    public Task InvalidateUserCacheAsync(int userId)
    {
        var cacheKey = GetUserCacheKey(userId);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache for user {UserId}", userId);
        return Task.CompletedTask;
    }
    
    public async Task InvalidateRoleCacheAsync(int roleId)
    {
        var users = await _unitOfWork.Users.Query().AsNoTracking().ToListAsync();
        var userIds = users
            .Where(user => RbacAggregateJson.ReadUserRoles(user.RoleIdsJson, _serializer)
                .Roles.Any(role => role.RoleId == roleId))
            .Select(user => user.Id)
            .ToList();

        foreach (var userId in userIds)
            await InvalidateUserCacheAsync(userId);

        _logger.LogDebug("Invalidated cache for {Count} users with role {RoleId}", userIds.Count, roleId);
    }

    // ==================== Nhật ký kiểm toán ====================

    public async Task<(List<RbacAuditLog> Logs, int TotalCount)> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? entityType = null,
        int? entityId = null,
        int? changedByAdminId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        // Expand phase: dual-written RbacAuditLogs remain the admin read path until Task 9.
        IQueryable<RbacAuditLog> query = _unitOfWork.RbacAuditLogs
            .Query()
            .Include(item => item.ChangedByAdmin);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(item => item.EntityType == entityType);
        if (entityId.HasValue)
            query = query.Where(item => item.EntityId == entityId.Value);
        if (changedByAdminId.HasValue)
            query = query.Where(item => item.ChangedByAdminId == changedByAdminId.Value);
        if (startDate.HasValue)
            query = query.Where(item => item.ChangedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(item => item.ChangedAt <= endDate.Value);

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(item => item.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logs, totalCount);
    }
}
