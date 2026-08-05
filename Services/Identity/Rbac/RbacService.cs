using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
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
    private const string CacheKeyPrefix = "rbac:user:";
    private const string CacheKeySuffix = ":permissions";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public RbacService(
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        ILogger<RbacService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
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
        var auditLog = new RbacAuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ChangedByAdminId = adminId,
            ChangedAt = DateTime.UtcNow,
            OldValue = oldValue,
            NewValue = newValue
        };
        
        await _unitOfWork.RbacAuditLogs.AddAsync(auditLog);
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
        
        // Get all active roles for the user
        var userRoles = await _unitOfWork.UserRoleMappings
            .Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync();
        
        if (!userRoles.Any())
        {
            _logger.LogDebug("User {UserId} has no active roles", userId);
            return new List<string>();
        }
        
        // Get all permissions from those roles
        var permissions = await _unitOfWork.RolePermissions
            .Query()
            .Include(rp => rp.Permission)
            .Where(rp => userRoles.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync();
        
        // Cache the result for 5 minutes
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
        
        // Check if role is assigned to any users
        var hasUsers = await _unitOfWork.UserRoleMappings
            .Query()
            .AnyAsync(ur => ur.RoleId == roleId);
            
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
        
        // Check if permission is assigned to any roles
        var hasRoles = await _unitOfWork.RolePermissions
            .Query()
            .AnyAsync(rp => rp.PermissionId == permissionId);
            
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
        // Validate role exists and is active
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
        if (role == null)
        {
            _logger.LogWarning("Attempt to assign permission to non-existent role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Role not found");
        }
        
        if (!role.IsActive)
        {
            _logger.LogWarning("Attempt to assign permission to inactive role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Cannot assign permission to inactive role");
        }
        
        // Validate permission exists
        var permission = await _unitOfWork.Permissions.GetByIdAsync(permissionId);
        if (permission == null)
        {
            _logger.LogWarning("Attempt to assign non-existent permission {PermissionId} to role {RoleId} by admin {AdminId}", permissionId, roleId, adminId);
            throw new InvalidOperationException("Permission not found");
        }
        
        // Check if already assigned (idempotence)
        var existing = await _unitOfWork.RolePermissions
            .Query()
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
            
        if (existing != null)
        {
            _logger.LogDebug("Permission {PermissionId} already assigned to role {RoleId}", permissionId, roleId);
            return; // Idempotent - no error, just return
        }
        
        var rolePermission = new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            AssignedAt = DateTime.UtcNow,
            AssignedByAdminId = adminId
        };
        
        await _unitOfWork.RolePermissions.AddAsync(rolePermission);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            "Assign",
            "RolePermission",
            rolePermission.Id,
            adminId,
            null,
            JsonSerializer.Serialize(new { RoleId = roleId, PermissionId = permissionId, PermissionName = permission.Name })
        );
        await _unitOfWork.SaveChangesAsync();
        
        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId);
        
        _logger.LogInformation("Permission {PermissionId} assigned to role {RoleId} by admin {AdminId}", permissionId, roleId, adminId);
    }
    
    public async Task AssignPermissionsToRoleAsync(int roleId, List<int> permissionIds, int adminId)
    {
        if (permissionIds == null || !permissionIds.Any())
            return;

        // Validate role exists and is active
        var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
        if (role == null)
        {
            _logger.LogWarning("Attempt to assign permissions to non-existent role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Role not found");
        }
        
        if (!role.IsActive)
        {
            _logger.LogWarning("Attempt to assign permissions to inactive role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("Cannot assign permission to inactive role");
        }

        // Validate permissions exist
        var uniquePermissionIds = permissionIds.Distinct().ToList();
        var permissions = await _unitOfWork.Permissions.Query()
            .Where(p => uniquePermissionIds.Contains(p.Id))
            .ToListAsync();

        if (permissions.Count != uniquePermissionIds.Count)
        {
            _logger.LogWarning("Attempt to assign non-existent permissions to role {RoleId} by admin {AdminId}", roleId, adminId);
            throw new InvalidOperationException("One or more permissions not found");
        }

        var permissionsDict = permissions.ToDictionary(p => p.Id);

        // Check if already assigned
        var existingMappings = await _unitOfWork.RolePermissions.Query()
            .Where(rp => rp.RoleId == roleId && uniquePermissionIds.Contains(rp.PermissionId))
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var toAssignIds = uniquePermissionIds.Except(existingMappings).ToList();
        if (!toAssignIds.Any())
        {
            return; // All are already assigned
        }

        var newRolePermissions = new List<RolePermission>();
        foreach (var permissionId in toAssignIds)
        {
            var rolePermission = new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                AssignedAt = DateTime.UtcNow,
                AssignedByAdminId = adminId
            };
            await _unitOfWork.RolePermissions.AddAsync(rolePermission);
            newRolePermissions.Add(rolePermission);
        }

        // Save to generate IDs
        await _unitOfWork.SaveChangesAsync();

        // Create audit logs
        foreach (var rolePermission in newRolePermissions)
        {
            var permissionName = permissionsDict[rolePermission.PermissionId].Name;
            await CreateAuditLogAsync(
                "Assign",
                "RolePermission",
                rolePermission.Id,
                adminId,
                null,
                JsonSerializer.Serialize(new { RoleId = roleId, PermissionId = rolePermission.PermissionId, PermissionName = permissionName })
            );
        }

        // Save audit logs
        await _unitOfWork.SaveChangesAsync();
        
        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId);
        
        _logger.LogInformation("{Count} permissions assigned to role {RoleId} by admin {AdminId}", toAssignIds.Count, roleId, adminId);
    }
    
    public async Task RevokePermissionFromRoleAsync(int roleId, int permissionId, int adminId)
    {
        var rolePermission = await _unitOfWork.RolePermissions
            .Query()
            .Include(rp => rp.Permission)
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
            
        if (rolePermission == null)
        {
            _logger.LogDebug("Permission {PermissionId} not assigned to role {RoleId}", permissionId, roleId);
            return; // Idempotent - no error if not found
        }
        
        var oldValue = JsonSerializer.Serialize(new { RoleId = roleId, PermissionId = permissionId, PermissionName = rolePermission.Permission.Name });
        
        _unitOfWork.RolePermissions.Remove(rolePermission);
        await _unitOfWork.SaveChangesAsync();
        
        // Create audit log
        await CreateAuditLogAsync(
            "Revoke",
            "RolePermission",
            rolePermission.Id,
            adminId,
            oldValue,
            null
        );
        await _unitOfWork.SaveChangesAsync();
        
        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId);
        
        _logger.LogInformation("Permission {PermissionId} revoked from role {RoleId} by admin {AdminId}", permissionId, roleId, adminId);
    }
    
    public async Task<List<Permission>> GetRolePermissionsAsync(int roleId)
    {
        return await _unitOfWork.RolePermissions
            .Query()
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
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

        // Enforce single-role restriction
        var uniqueRoleIds = roleIds.Distinct().ToList();
        if (uniqueRoleIds.Count > 1)
        {
            throw new InvalidOperationException("Users can only have a single role assigned.");
        }

        // Validate user exists
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Attempt to assign roles to non-existent user {UserId} by admin {AdminId}", userId, adminId);
            throw new InvalidOperationException("User not found");
        }

        // Get existing role mappings for user
        var existingMappings = await _unitOfWork.UserRoleMappings.Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .ToListAsync();

        var existingRoleIds = existingMappings.Select(m => m.RoleId).ToList();

        // Load all role info in batch for validation and audit logs
        var allRoleIds = uniqueRoleIds.Union(existingRoleIds).Distinct().ToList();
        var roles = await _unitOfWork.Roles.Query()
            .Where(r => allRoleIds.Contains(r.Id))
            .ToListAsync();

        var rolesDict = roles.ToDictionary(r => r.Id);

        // Validate role(s) to add
        foreach (var reqRoleId in uniqueRoleIds)
        {
            if (!rolesDict.TryGetValue(reqRoleId, out var r))
            {
                _logger.LogWarning("Attempt to assign non-existent roles to user {UserId} by admin {AdminId}", userId, adminId);
                throw new InvalidOperationException("One or more roles not found");
            }

            if (!r.IsActive)
            {
                _logger.LogWarning("Attempt to assign inactive roles to user {UserId} by admin {AdminId}", userId, adminId);
                throw new InvalidOperationException("Cannot assign inactive role");
            }
        }

        var toAddIds = uniqueRoleIds.Except(existingRoleIds).ToList();
        var toRemove = existingMappings.Where(m => !uniqueRoleIds.Contains(m.RoleId)).ToList();

        if (!toAddIds.Any() && !toRemove.Any())
        {
            return; // No changes needed
        }

        // Wrap the whole multi-write flow in a single transaction so any failure rolls back
        // mapping changes, audit logs, and the legacy User.Role sync together.
        var supportsTransactions = !(_unitOfWork.DatabaseProviderName?.Contains("InMemory") ?? false);
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (supportsTransactions)
        {
            transaction = await _unitOfWork.BeginTransactionAsync();
        }

        try
        {
            // Stage removals
            if (toRemove.Any())
            {
                _unitOfWork.UserRoleMappings.RemoveRange(toRemove);
                foreach (var mapping in toRemove)
                {
                    var roleName = mapping.Role?.Name ?? (rolesDict.TryGetValue(mapping.RoleId, out var r) ? r.Name : "Unknown");
                    await CreateAuditLogAsync(
                        "Revoke",
                        "UserRole",
                        mapping.Id,
                        adminId,
                        JsonSerializer.Serialize(new { UserId = userId, RoleId = mapping.RoleId, RoleName = roleName }),
                        null
                    );
                }
            }

            // Stage additions
            var newMappings = new List<UserRoleMapping>();
            if (toAddIds.Any())
            {
                foreach (var roleId in toAddIds)
                {
                    var userRole = new UserRoleMapping
                    {
                        UserId = userId,
                        RoleId = roleId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedByAdminId = adminId
                    };
                    await _unitOfWork.UserRoleMappings.AddAsync(userRole);
                    newMappings.Add(userRole);
                }
            }

            // Persist mapping changes so new IDs are available for audit logs.
            await _unitOfWork.SaveChangesAsync();

            // Stage assignment audit logs (now that we have the new mapping IDs).
            if (newMappings.Any())
            {
                foreach (var mapping in newMappings)
                {
                    var roleName = rolesDict.TryGetValue(mapping.RoleId, out var r) ? r.Name : "Unknown";
                    await CreateAuditLogAsync(
                        "Assign",
                        "UserRole",
                        mapping.Id,
                        adminId,
                        null,
                        JsonSerializer.Serialize(new { UserId = userId, RoleId = mapping.RoleId, RoleName = roleName })
                    );
                }
                await _unitOfWork.SaveChangesAsync();
            }

            // Sync legacy User.Role field for backward compatibility. Runs inside the transaction;
            // its internal SaveChangesAsync participates in the same unit of work.
            await SyncUserLegacyRoleAsync(userId);

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }

        // Invalidate cache for this user
        await InvalidateUserCacheAsync(userId);

        _logger.LogInformation("Roles updated for user {UserId} by admin {AdminId}. Added: {AddedCount}, Removed: {RemovedCount}",
            userId, adminId, toAddIds.Count, toRemove.Count);
    }
    
    public async Task RevokeRoleFromUserAsync(int userId, int roleId, int adminId)
    {
        var userRole = await _unitOfWork.UserRoleMappings
            .Query()
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            
        if (userRole == null)
        {
            _logger.LogDebug("Role {RoleId} not assigned to user {UserId}", roleId, userId);
            return; // Idempotent - no error if not found
        }
        
        var oldValue = JsonSerializer.Serialize(new { UserId = userId, RoleId = roleId, RoleName = userRole.Role.Name });
        
        _unitOfWork.UserRoleMappings.Remove(userRole);
        await _unitOfWork.SaveChangesAsync();
        
        // Sync legacy User.Role field for backward compatibility
        await SyncUserLegacyRoleAsync(userId);
        
        // Create audit log
        await CreateAuditLogAsync(
            "Revoke",
            "UserRole",
            userRole.Id,
            adminId,
            oldValue,
            null
        );
        await _unitOfWork.SaveChangesAsync();
        
        // Invalidate cache for this user
        await InvalidateUserCacheAsync(userId);
        
        _logger.LogInformation("Role {RoleId} revoked from user {UserId} by admin {AdminId}", roleId, userId, adminId);
    }
    
    public async Task<List<Role>> GetUserRolesAsync(int userId)
    {
        return await _unitOfWork.UserRoleMappings
            .Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .OrderBy(r => r.Name)
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
        // Get all users with this role
        var userIds = await _unitOfWork.UserRoleMappings
            .Query()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();
        
        // Invalidate cache for each user
        foreach (var userId in userIds)
        {
            await InvalidateUserCacheAsync(userId);
        }
        
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
        IQueryable<RbacAuditLog> query = _unitOfWork.RbacAuditLogs
            .Query()
            .Include(a => a.ChangedByAdmin);
        
        // Apply filters
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }
        
        if (entityId.HasValue)
        {
            query = query.Where(a => a.EntityId == entityId.Value);
        }
        
        if (changedByAdminId.HasValue)
        {
            query = query.Where(a => a.ChangedByAdminId == changedByAdminId.Value);
        }
        
        if (startDate.HasValue)
        {
            query = query.Where(a => a.ChangedAt >= startDate.Value);
        }
        
        if (endDate.HasValue)
        {
            query = query.Where(a => a.ChangedAt <= endDate.Value);
        }
        
        // Get total count
        var totalCount = await query.CountAsync();
        
        // Sort by ChangedAt descending (newest first) and apply pagination
        var logs = await query
            .OrderByDescending(a => a.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (logs, totalCount);
    }
}
