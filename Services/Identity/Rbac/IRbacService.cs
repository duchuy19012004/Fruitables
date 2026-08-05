using Fruitables.Models;

namespace Fruitables.Services.Identity.Rbac;

/// <summary>
/// Interface for RBAC (Role-Based Access Control) Service
/// Provides functionality for managing roles, permissions, and authorization
/// </summary>
public interface IRbacService
{
    // ==================== Kiểm tra quyền hạn ====================
    
    /// <summary>
    /// Check if a user has a specific permission
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="permissionName">Permission name (format: module.action)</param>
    /// <returns>True if user has the permission, false otherwise</returns>
    Task<bool> HasPermissionAsync(int userId, string permissionName);
    
    /// <summary>
    /// Check if a user has any of the specified permissions (OR logic)
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="permissionNames">Array of permission names</param>
    /// <returns>True if user has at least one permission, false otherwise</returns>
    Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionNames);
    
    /// <summary>
    /// Check if a user has all of the specified permissions (AND logic)
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="permissionNames">Array of permission names</param>
    /// <returns>True if user has all permissions, false otherwise</returns>
    Task<bool> HasAllPermissionsAsync(int userId, params string[] permissionNames);
    
    /// <summary>
    /// Get all permissions for a user (aggregated from all their roles)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of permission names</returns>
    Task<List<string>> GetUserPermissionsAsync(int userId);
    
    // ==================== Quản lý vai trò ====================
    
    /// <summary>
    /// Create a new role
    /// </summary>
    /// <param name="name">Role name (must be unique)</param>
    /// <param name="description">Role description</param>
    /// <param name="adminId">ID of admin creating the role</param>
    /// <returns>Created role</returns>
    Task<Role> CreateRoleAsync(string name, string? description, int adminId);
    
    /// <summary>
    /// Update an existing role
    /// </summary>
    /// <param name="roleId">Role ID to update</param>
    /// <param name="name">New role name</param>
    /// <param name="description">New role description</param>
    /// <param name="adminId">ID of admin updating the role</param>
    /// <returns>Updated role</returns>
    Task<Role> UpdateRoleAsync(int roleId, string name, string? description, int adminId);
    
    /// <summary>
    /// Delete a role (only if not assigned to any users)
    /// </summary>
    /// <param name="roleId">Role ID to delete</param>
    /// <param name="adminId">ID of admin deleting the role</param>
    Task DeleteRoleAsync(int roleId, int adminId);
    
    /// <summary>
    /// Toggle role active status
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="isActive">New active status</param>
    /// <param name="adminId">ID of admin toggling the status</param>
    /// <returns>Updated role</returns>
    Task<Role> ToggleRoleActiveAsync(int roleId, bool isActive, int adminId);
    
    /// <summary>
    /// Get role by ID
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>Role or null if not found</returns>
    Task<Role?> GetRoleByIdAsync(int roleId);
    
    /// <summary>
    /// Get all roles
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive roles</param>
    /// <returns>List of roles</returns>
    Task<List<Role>> GetAllRolesAsync(bool includeInactive = false);
    
    /// <summary>
    /// Get roles with pagination and search
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="searchTerm">Optional search term for role name</param>
    /// <returns>Tuple of roles list and total count</returns>
    Task<(List<Role> Roles, int TotalCount)> GetRolesPagedAsync(int page, int pageSize, string? searchTerm = null);
    
    // ==================== Quản lý quyền hạn ====================
    
    /// <summary>
    /// Create a new permission
    /// </summary>
    /// <param name="name">Permission name (format: module.action)</param>
    /// <param name="module">Module name (e.g., products, orders)</param>
    /// <param name="description">Permission description</param>
    /// <param name="adminId">ID of admin creating the permission</param>
    /// <returns>Created permission</returns>
    Task<Permission> CreatePermissionAsync(string name, string module, string? description, int adminId);
    
    /// <summary>
    /// Get permission by ID
    /// </summary>
    /// <param name="permissionId">Permission ID</param>
    /// <returns>Permission or null if not found</returns>
    Task<Permission?> GetPermissionByIdAsync(int permissionId);
    
    /// <summary>
    /// Get all permissions
    /// </summary>
    /// <returns>List of all permissions</returns>
    Task<List<Permission>> GetAllPermissionsAsync();
    
    /// <summary>
    /// Get permissions by module
    /// </summary>
    /// <param name="module">Module name</param>
    /// <returns>List of permissions in the module</returns>
    Task<List<Permission>> GetPermissionsByModuleAsync(string module);
    
    /// <summary>
    /// Get all permissions grouped by module
    /// </summary>
    /// <returns>Dictionary with module name as key and list of permissions as value</returns>
    Task<Dictionary<string, List<Permission>>> GetPermissionsGroupedByModuleAsync();
    
    /// <summary>
    /// Delete a permission (only if not assigned to any roles)
    /// </summary>
    /// <param name="permissionId">Permission ID to delete</param>
    /// <param name="adminId">ID of admin deleting the permission</param>
    Task DeletePermissionAsync(int permissionId, int adminId);
    
    // ==================== Gán quyền hạn cho vai trò ====================
    
    /// <summary>
    /// Assign a permission to a role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="permissionId">Permission ID</param>
    /// <param name="adminId">ID of admin performing the assignment</param>
    Task AssignPermissionToRoleAsync(int roleId, int permissionId, int adminId);
    
    /// <summary>
    /// Assign multiple permissions to a role (bulk operation)
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="permissionIds">List of permission IDs</param>
    /// <param name="adminId">ID of admin performing the assignment</param>
    Task AssignPermissionsToRoleAsync(int roleId, List<int> permissionIds, int adminId);
    
    /// <summary>
    /// Revoke a permission from a role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="permissionId">Permission ID</param>
    /// <param name="adminId">ID of admin performing the revocation</param>
    Task RevokePermissionFromRoleAsync(int roleId, int permissionId, int adminId);
    
    /// <summary>
    /// Get all permissions assigned to a role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>List of permissions</returns>
    Task<List<Permission>> GetRolePermissionsAsync(int roleId);
    
    // ==================== Gán vai trò cho người dùng ====================
    
    /// <summary>
    /// Assign a role to a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleId">Role ID</param>
    /// <param name="adminId">ID of admin performing the assignment</param>
    Task AssignRoleToUserAsync(int userId, int roleId, int adminId);
    
    /// <summary>
    /// Assign multiple roles to a user (bulk operation)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleIds">List of role IDs</param>
    /// <param name="adminId">ID of admin performing the assignment</param>
    Task AssignRolesToUserAsync(int userId, List<int> roleIds, int adminId);
    
    /// <summary>
    /// Revoke a role from a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleId">Role ID</param>
    /// <param name="adminId">ID of admin performing the revocation</param>
    Task RevokeRoleFromUserAsync(int userId, int roleId, int adminId);
    
    /// <summary>
    /// Get all roles assigned to a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of roles</returns>
    Task<List<Role>> GetUserRolesAsync(int userId);
    
    // ==================== Quản lý cache ====================
    
    /// <summary>
    /// Invalidate permission cache for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    Task InvalidateUserCacheAsync(int userId);
    
    /// <summary>
    /// Invalidate permission cache for all users with a specific role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    Task InvalidateRoleCacheAsync(int roleId);
    
    // ==================== Nhật ký kiểm toán ====================
    
    /// <summary>
    /// Get audit logs with filtering and pagination
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="entityType">Optional filter by entity type (Role, Permission, UserRole, RolePermission)</param>
    /// <param name="entityId">Optional filter by entity ID</param>
    /// <param name="changedByAdminId">Optional filter by admin who made the change</param>
    /// <param name="startDate">Optional filter by start date</param>
    /// <param name="endDate">Optional filter by end date</param>
    /// <returns>Tuple of audit logs list and total count</returns>
    Task<(List<RbacAuditLog> Logs, int TotalCount)> GetAuditLogsAsync(
        int page, 
        int pageSize, 
        string? entityType = null, 
        int? entityId = null, 
        int? changedByAdminId = null, 
        DateTime? startDate = null, 
        DateTime? endDate = null);
}
