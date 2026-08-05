using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Fruitables.Services.Identity.Rbac;

namespace Fruitables.Extensions;

/// <summary>
/// Extension methods for User model to support backward compatibility with legacy UserRole enum
/// </summary>
public static class UserRbacExtensions
{
    /// <summary>
    /// Get the legacy UserRole enum value based on RBAC roles
    /// Priority: SuperAdmin > Admin > Customer
    /// </summary>
    /// <param name="user">User instance</param>
    /// <param name="rbacService">RBAC service to query user roles</param>
    /// <returns>Legacy UserRole enum value</returns>
    public static async Task<UserRole> GetLegacyRoleAsync(this User user, IRbacService rbacService)
    {
        var roles = await rbacService.GetUserRolesAsync(user.Id);
        
        // Priority: SuperAdmin > Admin > Customer
        if (roles.Any(r => r.Name == "SuperAdmin"))
            return UserRole.SuperAdmin;
        if (roles.Any(r => r.Name == "Admin"))
            return UserRole.Admin;
        
        return UserRole.Customer;
    }
    
    /// <summary>
    /// Synchronize the legacy User.Role column with RBAC roles
    /// Updates User.Role to match the highest priority RBAC role
    /// </summary>
    /// <param name="user">User instance</param>
    /// <param name="context">Database context</param>
    /// <param name="rbacService">RBAC service to query user roles</param>
    public static async Task SyncLegacyRoleAsync(this User user, ApplicationDbContext context, IRbacService rbacService)
    {
        var legacyRole = await user.GetLegacyRoleAsync(rbacService);
        if (user.Role != legacyRole)
        {
            user.Role = legacyRole;
            user.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }
}
