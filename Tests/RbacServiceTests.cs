using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fruitables.Services.Identity.Rbac;

namespace Fruitables.Tests
{
    public class RbacServiceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task AssignRolesToUserAsync_MultipleRoles_ThrowsInvalidOperationException()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options);

            var cacheMock = new Mock<IMemoryCache>();
            var loggerMock = new Mock<ILogger<RbacService>>();
            var rbacService = new RbacService(context, cacheMock.Object, loggerMock.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                rbacService.AssignRolesToUserAsync(1, new List<int> { 1, 2 }, 100));

            Assert.Contains("single role assigned", exception.Message);
        }

        [Fact]
        public async Task AssignRolesToUserAsync_SingleRole_RemovesPreviousRoles_AndAddsNewOne()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options);

            // Use Ids that do not collide with the model seed (Admin=1, SuperAdmin=2).
            var user = new User
            {
                Id = 100,
                Name = "Test User",
                Email = "test@example.com",
                Password = "hashed_password",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);

            var oldRole = new Role { Id = 10, Name = "OldRole", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var newRole = new Role { Id = 20, Name = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            context.Roles.AddRange(oldRole, newRole);

            var oldMapping = new UserRoleMapping { Id = 1, UserId = 100, RoleId = 10, AssignedAt = DateTime.UtcNow, AssignedByAdminId = 200 };
            context.UserRoleMappings.Add(oldMapping);
            await context.SaveChangesAsync();


            var cacheMock = new Mock<IMemoryCache>();
            object? value;
            cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out value)).Returns(false);
            cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

            var loggerMock = new Mock<ILogger<RbacService>>();
            var rbacService = new RbacService(context, cacheMock.Object, loggerMock.Object);

            // Act
            await rbacService.AssignRolesToUserAsync(100, new List<int> { 20 }, 200);

            // Assert: chỉ còn mapping mới
            var mappings = await context.UserRoleMappings.Where(m => m.UserId == 100).ToListAsync();
            Assert.Single(mappings);
            Assert.Equal(20, mappings[0].RoleId);

            // Verify legacy User.Role được sync — "Admin" → UserRole.Admin
            var updatedUser = await context.Users.FindAsync(100);
            Assert.NotNull(updatedUser);
            Assert.Equal(UserRole.Admin, updatedUser.Role);

            // Verify audit logs được tạo
            var auditLogs = await context.RbacAuditLogs.ToListAsync();

            var revokeLog = auditLogs.FirstOrDefault(l => l.Action == "Revoke");
            var assignLog = auditLogs.FirstOrDefault(l => l.Action == "Assign");

            Assert.NotNull(revokeLog);
            Assert.Contains("\"RoleId\":10", revokeLog.OldValue);

            Assert.NotNull(assignLog);
            Assert.Contains("\"RoleId\":20", assignLog.NewValue);
        }

        [Fact]
        public async Task AssignRoleToUserAsync_OnSqlite_DelegatesToTransactionalPath()
        {
            // Verify the single-role API also runs the full atomic flow on SQLite.
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);

            context.Users.Add(new User
            {
                Id = 100,
                Name = "Sqlite User",
                Email = "sqlite@example.com",
                Password = "hashed_password",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Users.Add(new User
            {
                Id = 200,
                Name = "Test Admin",
                Email = "admin200@example.com",
                Password = "hashed",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Roles.AddRange(
                new Role { Id = 10, Name = "OldRole", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Role { Id = 20, Name = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            context.UserRoleMappings.Add(new UserRoleMapping
            {
                Id = 1,
                UserId = 100,
                RoleId = 10,
                AssignedAt = DateTime.UtcNow,
                AssignedByAdminId = 200
            });
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IMemoryCache>();
            object? value;
            cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out value)).Returns(false);
            cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());
            var loggerMock = new Mock<ILogger<RbacService>>();
            var rbacService = new RbacService(context, cacheMock.Object, loggerMock.Object);

            await rbacService.AssignRoleToUserAsync(100, 20, 200);

            Assert.Equal(UserRole.Admin, (await context.Users.FindAsync(100))!.Role);
            Assert.Single(await context.UserRoleMappings.Where(m => m.UserId == 100).ToListAsync());
            Assert.Equal(20, (await context.UserRoleMappings.FirstAsync(m => m.UserId == 100)).RoleId);
        }

        [Fact]
        public async Task AssignRolesToUserAsync_OnSqlite_MapsAndSyncsLegacyRole()
        {
            // Run the same scenario on SQLite (transaction-capable) to validate the
            // atomic flow with real provider behavior.
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);

            // Use Ids that do not collide with the model seed (Admin=1, SuperAdmin=2).
            context.Users.Add(new User
            {
                Id = 100,
                Name = "Sqlite User",
                Email = "sqlite@example.com",
                Password = "hashed_password",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Users.Add(new User
            {
                Id = 200,
                Name = "Test Admin",
                Email = "admin200@example.com",
                Password = "hashed",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Roles.AddRange(
                new Role { Id = 10, Name = "OldRole", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Role { Id = 20, Name = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            context.UserRoleMappings.Add(new UserRoleMapping
            {
                Id = 1,
                UserId = 100,
                RoleId = 10,
                AssignedAt = DateTime.UtcNow,
                AssignedByAdminId = 200
            });
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IMemoryCache>();
            object? value;
            cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out value)).Returns(false);
            cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());
            var loggerMock = new Mock<ILogger<RbacService>>();
            var rbacService = new RbacService(context, cacheMock.Object, loggerMock.Object);

            await rbacService.AssignRolesToUserAsync(100, new List<int> { 20 }, 200);

            Assert.Equal(UserRole.Admin, (await context.Users.FindAsync(100))!.Role);
            Assert.Single(await context.UserRoleMappings.Where(m => m.UserId == 100).ToListAsync());
        }
    }
}
