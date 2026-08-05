using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Fruitables.Services.Identity.Rbac;

namespace Fruitables.Services.Infrastructure;

public class MigrationService : IMigrationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRbacService _rbacService;
    private readonly ILogger<MigrationService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public MigrationService(
        IUnitOfWork unitOfWork,
        IRbacService rbacService,
        ILogger<MigrationService> logger,
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory)
    {
        _unitOfWork = unitOfWork;
        _rbacService = rbacService;
        _logger = logger;
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    private class OldAddressRow
    {
        public int Id { get; set; }
        public int ProvinceCode { get; set; }
        public string ProvinceName { get; set; } = string.Empty;
        public int DistrictCode { get; set; }
        public string DistrictName { get; set; } = string.Empty;
        public int WardCode { get; set; }
        public string WardName { get; set; } = string.Empty;
    }
    
    public async Task<MigrationResult> MigrateToRbacAsync()
    {
        var result = new MigrationResult
        {
            CompletedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting RBAC migration...");

            // Step 1: Seed default roles, permissions, and mappings
            await SeedDefaultRolesAsync();
            await SeedDefaultPermissionsAsync();
            await SeedRolePermissionMappingsAsync();
            
            // Step 2: Seed default test users
            await SeedDefaultUsersAsync();

            // Step 3: Migrate existing users
            var users = await _unitOfWork.Users
                .Query()
                .Include(u => u.UserRoleMappings)
                .ToListAsync();

            _logger.LogInformation("Found {UserCount} users to migrate", users.Count);

            int usersProcessed = 0;
            var errors = new List<string>();

            foreach (var user in users)
            {
                try
                {
                    // Check if user already has RBAC role mapping
                    if (user.UserRoleMappings.Any())
                    {
                        _logger.LogDebug("User {UserId} already has RBAC role mappings, skipping", user.Id);
                        continue;
                    }

                    // Get the role based on legacy UserRole enum
                    var roleName = user.Role.ToString();
                    var roles = await _unitOfWork.Roles
                        .FindAsync(r => r.Name == roleName && r.IsActive);
                    
                    var role = roles.FirstOrDefault();
                    
                    if (role == null)
                    {
                        var error = $"Role '{roleName}' not found for user {user.Id}";
                        _logger.LogWarning(error);
                        errors.Add(error);
                        continue;
                    }

                    // Create UserRoleMapping
                    var userRoleMapping = new UserRoleMapping
                    {
                        UserId = user.Id,
                        RoleId = role.Id,
                        AssignedAt = DateTime.UtcNow,
                        AssignedByAdminId = null // System migration
                    };

                    await _unitOfWork.UserRoleMappings.AddAsync(userRoleMapping);
                    usersProcessed++;

                    _logger.LogDebug("Migrated user {UserId} to role {RoleName}", user.Id, roleName);
                }
                catch (Exception ex)
                {
                    var error = $"Error migrating user {user.Id}: {ex.Message}";
                    _logger.LogError(ex, error);
                    errors.Add(error);
                }
            }

            // Save all changes
            await _unitOfWork.SaveChangesAsync();

            result.Success = errors.Count == 0;
            result.UsersProcessed = usersProcessed;
            result.Errors = errors;

            _logger.LogInformation(
                "RBAC migration completed. Users processed: {UsersProcessed}, Errors: {ErrorCount}",
                usersProcessed,
                errors.Count
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during RBAC migration");
            result.Success = false;
            result.Errors.Add($"Fatal error: {ex.Message}");
            return result;
        }
    }
    
    public async Task<MigrationResult> RollbackToLegacyAsync()
    {
        var result = new MigrationResult
        {
            CompletedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting RBAC rollback...");

            // Get all UserRole mappings
            var userRoleMappings = await _unitOfWork.UserRoleMappings
                .Query()
                .ToListAsync();

            _logger.LogInformation("Found {MappingCount} user role mappings to remove", userRoleMappings.Count);

            // Remove all UserRole mappings
            _unitOfWork.UserRoleMappings.RemoveRange(userRoleMappings);
            
            // Save changes
            await _unitOfWork.SaveChangesAsync();

            result.Success = true;
            result.UsersProcessed = userRoleMappings.Select(urm => urm.UserId).Distinct().Count();

            _logger.LogInformation(
                "RBAC rollback completed. Removed {MappingCount} mappings affecting {UserCount} users",
                userRoleMappings.Count,
                result.UsersProcessed
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during RBAC rollback");
            result.Success = false;
            result.Errors.Add($"Fatal error: {ex.Message}");
            return result;
        }
    }
    
    public async Task<MigrationStatus> GetMigrationStatusAsync()
    {
        try
        {
            // Get total number of users
            var totalUsers = await _unitOfWork.Users.CountAsync();
            
            // Get number of users with RBAC role mappings
            var migratedUsers = await _unitOfWork.UserRoleMappings
                .Query()
                .Select(urm => urm.UserId)
                .Distinct()
                .CountAsync();
            
            // Get last migration date (most recent UserRoleMapping creation)
            var lastMigrationDate = await _unitOfWork.UserRoleMappings
                .Query()
                .OrderByDescending(urm => urm.AssignedAt)
                .Select(urm => (DateTime?)urm.AssignedAt)
                .FirstOrDefaultAsync();
            
            return new MigrationStatus
            {
                IsCompleted = totalUsers > 0 && migratedUsers == totalUsers,
                TotalUsers = totalUsers,
                MigratedUsers = migratedUsers,
                LastMigrationDate = lastMigrationDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration status");
            return new MigrationStatus
            {
                IsCompleted = false,
                TotalUsers = 0,
                MigratedUsers = 0,
                LastMigrationDate = null
            };
        }
    }
    
    public async Task<MigrationResult> MigrateAddressesToTwoLevelAsync()
    {
        var result = new MigrationResult { CompletedAt = DateTime.UtcNow };

        // GUARD: Kiểm tra xem DB đã được migrate sang schema 2 cấp chưa.
        // Sau khi migration SwitchToTwoLevelAddress chạy, các cột DistrictCode/DistrictName/WardCode
        // đã bị xóa. Gọi hàm này sau đó sẽ crash với lỗi "Invalid column name".
        try
        {
            using var checkConn = _context.Database.GetDbConnection();
            await checkConn.OpenAsync();
            using var checkCmd = checkConn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Addresses' AND COLUMN_NAME='DistrictCode'";
            var colExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);
            if (colExists == 0)
            {
                result.Success = true;
                result.Errors.Add("Skipped: DB schema already on 2-level (DistrictCode column not found). No action needed.");
                _logger.LogWarning("MigrateAddressesToTwoLevelAsync: DB already migrated. Skipping.");
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Schema check failed: {ex.Message}");
            return result;
        }

        try
        {
            _logger.LogInformation("Starting address 3-level → 2-level migration...");

            var cache = new Dictionary<string, (string provinceId, string communeId, string communeName)>();
            var errors = new List<string>();
            int processed = 0;
            int skipped = 0;

            using var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, ProvinceCode, ProvinceName, DistrictCode, DistrictName, WardCode, WardName FROM Addresses";
            using var reader = await cmd.ExecuteReaderAsync();

            var client = _httpClientFactory.CreateClient("AddressKit");
            var batch = new List<Address>();

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var provinceCode = reader.GetInt32(1);
                var provinceName = reader.GetString(2);
                var districtCode = reader.GetInt32(3);
                var districtName = reader.GetString(4);
                var wardCode = reader.GetInt32(5);
                var wardName = reader.GetString(6);

                var key = $"{provinceCode}-{districtCode}-{wardCode}";

                if (!cache.TryGetValue(key, out var converted))
                {
                    var req = new ConvertAddressRequest
                    {
                        ProvinceCode = provinceCode,
                        DistrictCode = districtCode,
                        WardCode = wardCode
                    };

                    try
                    {
                        var httpResponse = await client.PostAsJsonAsync("convert", req);

                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            var body = await httpResponse.Content.ReadAsStringAsync();
                            errors.Add($"API error for address {id} ({provinceName}/{districtName}/{wardName}): {httpResponse.StatusCode} {body}");
                            skipped++;
                            continue;
                        }

                        var conv = await httpResponse.Content.ReadFromJsonAsync<ConvertAddressResponse>();

                        if (conv == null || string.IsNullOrEmpty(conv.CommuneId))
                        {
                            errors.Add($"Empty response for address {id}");
                            skipped++;
                            continue;
                        }

                        converted = (conv.ProvinceId, conv.CommuneId, conv.CommuneName);
                        cache[key] = converted;
                    }
                    catch (HttpRequestException ex)
                    {
                        errors.Add($"HTTP error for address {id}: {ex.Message}");
                        skipped++;
                        continue;
                    }
                }

                var address = await _unitOfWork.Addresses.GetByIdAsync(id);
                if (address == null)
                {
                    errors.Add($"Address {id} not found in DB");
                    skipped++;
                    continue;
                }

                address.ProvinceCode = converted.provinceId;
                address.ProvinceName = provinceName;
                address.CommuneCode = converted.communeId;
                address.CommuneName = converted.communeName;
                batch.Add(address);
                processed++;

                if (batch.Count >= 100)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Migrated {Count} addresses so far...", processed);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            result.Success = errors.Count == 0;
            result.UsersProcessed = processed;
            result.Errors = errors;

            _logger.LogInformation(
                "Address migration done. Processed: {Processed}, Skipped: {Skipped}, Errors: {ErrorCount}",
                processed, skipped, errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during address migration");
            result.Success = false;
            result.Errors.Add($"Fatal: {ex.Message}");
            return result;
        }
    }

    public async Task SeedDefaultRolesAsync()
    {
        try
        {
            _logger.LogInformation("Seeding default roles...");

            var defaultRoles = new[]
            {
                new Role { Name = "Customer", Description = "Customer role with basic permissions", IsActive = true },
                new Role { Name = "Admin", Description = "Admin role with most permissions", IsActive = true },
                new Role { Name = "SuperAdmin", Description = "SuperAdmin role with all permissions", IsActive = true }
            };

            foreach (var role in defaultRoles)
            {
                var existingRole = await _unitOfWork.Roles
                    .FirstOrDefaultAsync(r => r.Name == role.Name);
                
                if (existingRole == null)
                {
                    await _unitOfWork.Roles.AddAsync(role);
                    _logger.LogInformation("Created role: {RoleName}", role.Name);
                }
                else
                {
                    _logger.LogDebug("Role already exists: {RoleName}", role.Name);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Default roles seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default roles");
            throw;
        }
    }
    
    public async Task SeedDefaultPermissionsAsync()
    {
        try
        {
            _logger.LogInformation("Seeding default permissions...");

            var defaultPermissions = new[]
            {
                // Products module
                new Permission { Name = "products.view", Description = "View products", Module = "products" },
                new Permission { Name = "products.create", Description = "Create products", Module = "products" },
                new Permission { Name = "products.update", Description = "Update products", Module = "products" },
                new Permission { Name = "products.delete", Description = "Delete products", Module = "products" },
                new Permission { Name = "products.manage_inventory", Description = "Manage product inventory", Module = "products" },
                
                // Orders module
                new Permission { Name = "orders.view_all", Description = "View all orders", Module = "orders" },
                new Permission { Name = "orders.view_own", Description = "View own orders", Module = "orders" },
                new Permission { Name = "orders.create", Description = "Create orders", Module = "orders" },
                new Permission { Name = "orders.update_status", Description = "Update order status", Module = "orders" },
                new Permission { Name = "orders.cancel", Description = "Cancel orders", Module = "orders" },
                new Permission { Name = "orders.refund", Description = "Process refunds", Module = "orders" },
                
                // Users module
                new Permission { Name = "users.view", Description = "View users", Module = "users" },
                new Permission { Name = "users.create", Description = "Create users", Module = "users" },
                new Permission { Name = "users.update", Description = "Update users", Module = "users" },
                new Permission { Name = "users.lock", Description = "Lock user accounts", Module = "users" },
                new Permission { Name = "users.unlock", Description = "Unlock user accounts", Module = "users" },
                new Permission { Name = "users.delete", Description = "Delete users", Module = "users" },
                
                // Reviews module
                new Permission { Name = "reviews.view", Description = "View reviews", Module = "reviews" },
                new Permission { Name = "reviews.create", Description = "Create reviews", Module = "reviews" },
                new Permission { Name = "reviews.edit_own", Description = "Edit own reviews", Module = "reviews" },
                new Permission { Name = "reviews.delete_own", Description = "Delete own reviews", Module = "reviews" },
                new Permission { Name = "reviews.moderate", Description = "Moderate reviews", Module = "reviews" },
                new Permission { Name = "reviews.delete", Description = "Delete reviews", Module = "reviews" },
                new Permission { Name = "reviews.view_reports", Description = "View review reports", Module = "reviews" },
                new Permission { Name = "reviews.view_statistics", Description = "View review statistics", Module = "reviews" },
                
                // Settings module
                new Permission { Name = "settings.view", Description = "View settings", Module = "settings" },
                new Permission { Name = "settings.update", Description = "Update settings", Module = "settings" },
                
                // Dashboard module
                new Permission { Name = "dashboard.view", Description = "View dashboard", Module = "dashboard" },
                new Permission { Name = "dashboard.view_statistics", Description = "View detailed statistics", Module = "dashboard" },
                
                // System module
                new Permission { Name = "system.manage", Description = "Manage entire system", Module = "system" },
                new Permission { Name = "system.view_logs", Description = "View system logs", Module = "system" },
                new Permission { Name = "system.manage_rbac", Description = "Manage roles and permissions", Module = "system" },

            };

            foreach (var permission in defaultPermissions)
            {
                var existingPermission = await _unitOfWork.Permissions
                    .FirstOrDefaultAsync(p => p.Name == permission.Name);
                
                if (existingPermission == null)
                {
                    await _unitOfWork.Permissions.AddAsync(permission);
                    _logger.LogDebug("Created permission: {PermissionName}", permission.Name);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Default permissions seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default permissions");
            throw;
        }
    }

    public async Task SeedRolePermissionMappingsAsync()
    {
        try
        {
            _logger.LogInformation("Seeding role-permission mappings...");

            // Get roles
            var customerRole = await _unitOfWork.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
            var adminRole = await _unitOfWork.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            var superAdminRole = await _unitOfWork.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");

            if (customerRole == null || adminRole == null || superAdminRole == null)
            {
                throw new InvalidOperationException("Default roles must be seeded before role-permission mappings");
            }

            // Get all permissions
            var allPermissions = await _unitOfWork.Permissions.GetAllAsync();
            var permissionDict = allPermissions.ToDictionary(p => p.Name, p => p);

            // Customer permissions
            var customerPermissions = new[]
            {
                "products.view",
                "orders.view_own",
                "orders.create",
                "reviews.view",
                "reviews.create",
                "reviews.edit_own",
                "reviews.delete_own"
            };

            await AssignPermissionsToRole(customerRole.Id, customerPermissions, permissionDict);

            // Admin permissions (all except system.manage)
            var adminPermissions = allPermissions
                .Where(p => p.Name != "system.manage")
                .Select(p => p.Name)
                .ToArray();

            await AssignPermissionsToRole(adminRole.Id, adminPermissions, permissionDict);

            // SuperAdmin permissions (all)
            var superAdminPermissions = allPermissions.Select(p => p.Name).ToArray();
            await AssignPermissionsToRole(superAdminRole.Id, superAdminPermissions, permissionDict);

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Role-permission mappings seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding role-permission mappings");
            throw;
        }
    }

    private async Task AssignPermissionsToRole(int roleId, string[] permissionNames, Dictionary<string, Permission> permissionDict)
    {
        foreach (var permissionName in permissionNames)
        {
            if (!permissionDict.TryGetValue(permissionName, out var permission))
            {
                _logger.LogWarning("Permission not found: {PermissionName}", permissionName);
                continue;
            }

            var existingMapping = await _unitOfWork.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permission.Id);

            if (existingMapping == null)
            {
                await _unitOfWork.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }
    }
    
    public async Task ResetToDefaultSeedDataAsync()
    {
        try
        {
            _logger.LogInformation("Resetting to default seed data...");

            // Remove all existing role-permission mappings
            var existingMappings = await _unitOfWork.RolePermissions.GetAllAsync();
            _unitOfWork.RolePermissions.RemoveRange(existingMappings);

            // Remove all existing permissions
            var existingPermissions = await _unitOfWork.Permissions.GetAllAsync();
            _unitOfWork.Permissions.RemoveRange(existingPermissions);

            // Remove all existing roles
            var existingRoles = await _unitOfWork.Roles.GetAllAsync();
            _unitOfWork.Roles.RemoveRange(existingRoles);

            await _unitOfWork.SaveChangesAsync();

            // Re-seed everything
            await SeedDefaultRolesAsync();
            await SeedDefaultPermissionsAsync();
            await SeedRolePermissionMappingsAsync();

            _logger.LogInformation("Reset to default seed data completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting to default seed data");
            throw;
        }
    }
    
    public async Task SeedDefaultUsersAsync()
    {
        try
        {
            _logger.LogInformation("Seeding default test users...");

            // Check if test users already exist
            var testEmails = new[] { "customer@gmail.com", "admin@gmail.com", "superadmin@gmail.com" };
            var existingUsers = await _unitOfWork.Users
                .Query()
                .Where(u => testEmails.Contains(u.Email))
                .ToListAsync();

            if (existingUsers.Any())
            {
                _logger.LogInformation("Test users already exist, skipping seed");
                return;
            }

            // Get roles
            var customerRole = await _unitOfWork.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
            var adminRole = await _unitOfWork.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            var superAdminRole = await _unitOfWork.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");

            if (customerRole == null || adminRole == null || superAdminRole == null)
            {
                throw new InvalidOperationException("Default roles must be seeded before creating test users");
            }

            // Password hashes for "Customer@123" and "Admin@123"
            var customerPasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123");
            var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");

            // Create test users
            var testUsers = new[]
            {
                new User
                {
                    Name = "Nguyễn Văn A",
                    Email = "customer@gmail.com",
                    Password = customerPasswordHash,
                    Role = UserRole.Customer,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new User
                {
                    Name = "Admin User",
                    Email = "admin@gmail.com",
                    Password = adminPasswordHash,
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new User
                {
                    Name = "Super Admin",
                    Email = "superadmin@gmail.com",
                    Password = adminPasswordHash,
                    Role = UserRole.SuperAdmin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            // Add users to database
            foreach (var user in testUsers)
            {
                await _unitOfWork.Users.AddAsync(user);
            }
            
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Test users created successfully");

            // Now assign RBAC roles to the newly created users
            var createdUsers = await _unitOfWork.Users
                .Query()
                .Where(u => testEmails.Contains(u.Email))
                .ToListAsync();

            foreach (var user in createdUsers)
            {
                int roleId;
                if (user.Email == "customer@gmail.com")
                    roleId = customerRole.Id;
                else if (user.Email == "admin@gmail.com")
                    roleId = adminRole.Id;
                else
                    roleId = superAdminRole.Id;

                var userRoleMapping = new UserRoleMapping
                {
                    UserId = user.Id,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByAdminId = null // System seed
                };

                await _unitOfWork.UserRoleMappings.AddAsync(userRoleMapping);
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("RBAC roles assigned to test users successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default test users");
            throw;
        }
    }
}
