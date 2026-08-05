namespace Fruitables.Services.Infrastructure;

/// <summary>
/// Interface for RBAC Migration Service
/// Provides functionality for migrating from legacy UserRole enum to RBAC system
/// </summary>
public interface IMigrationService
{
    // ==================== Các thao tác migration ====================
    
    /// <summary>
    /// Migrate all users from legacy UserRole enum to RBAC system
    /// </summary>
    /// <returns>Migration result with statistics and errors</returns>
    Task<MigrationResult> MigrateToRbacAsync();
    
    /// <summary>
    /// Rollback RBAC migration and restore legacy UserRole system
    /// </summary>
    /// <returns>Migration result with statistics and errors</returns>
    Task<MigrationResult> RollbackToLegacyAsync();
    
    /// <summary>
    /// Get current migration status
    /// </summary>
    /// <returns>Migration status information</returns>
    Task<MigrationStatus> GetMigrationStatusAsync();
    
    // ==================== Các thao tác seed ====================
    
    /// <summary>
    /// Seed default roles (Customer, Admin, SuperAdmin)
    /// </summary>
    Task SeedDefaultRolesAsync();
    
    /// <summary>
    /// Seed default permissions for all modules
    /// </summary>
    Task SeedDefaultPermissionsAsync();
    
    /// <summary>
    /// Seed role-permission mappings based on default configuration
    /// </summary>
    Task SeedRolePermissionMappingsAsync();
    
    /// <summary>
    /// Reset all RBAC data to default seed data
    /// </summary>
    Task ResetToDefaultSeedDataAsync();
    
    /// <summary>
    /// Seed default test users (Customer, Admin, SuperAdmin)
    /// </summary>
    Task SeedDefaultUsersAsync();

    /// <summary>
    /// Migrate existing addresses from 3-level (province/district/ward) to 2-level (province/commune).
    /// Calls AddressKit POST /convert for each address and updates ProvinceCode, CommuneCode, CommuneName.
    /// <b>Must be run BEFORE applying the SwitchToTwoLevelAddress EF migration.</b>
    /// </summary>
    Task<MigrationResult> MigrateAddressesToTwoLevelAsync();
}

/// <summary>
/// Result of a migration operation
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Whether the migration was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Number of users processed during migration
    /// </summary>
    public int UsersProcessed { get; set; }
    
    /// <summary>
    /// Number of roles created during migration
    /// </summary>
    public int RolesCreated { get; set; }
    
    /// <summary>
    /// Number of permissions created during migration
    /// </summary>
    public int PermissionsCreated { get; set; }
    
    /// <summary>
    /// List of errors encountered during migration
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Timestamp when migration completed
    /// </summary>
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Current status of RBAC migration
/// </summary>
public class MigrationStatus
{
    /// <summary>
    /// Whether migration has been completed
    /// </summary>
    public bool IsCompleted { get; set; }
    
    /// <summary>
    /// Total number of users in the system
    /// </summary>
    public int TotalUsers { get; set; }
    
    /// <summary>
    /// Number of users that have been migrated to RBAC
    /// </summary>
    public int MigratedUsers { get; set; }
    
    /// <summary>
    /// Date of last migration run
    /// </summary>
    public DateTime? LastMigrationDate { get; set; }
}
