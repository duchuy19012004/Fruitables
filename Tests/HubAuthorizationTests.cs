using Xunit;
using Moq;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Fruitables.Hubs;
using Fruitables.Services.Communications;
using Fruitables.Data;
using Fruitables.Models;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Chat.Conversation;

namespace Fruitables.Tests
{
    public class HubAuthorizationTests
    {
        private DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task JoinOrderGroup_Admin_CanJoinAnyOrder()
        {
            // Arrange
            var mockClients = new Mock<IHubCallerClients>();
            var mockGroupManager = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "100"),
                new Claim(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            mockContext.Setup(c => c.User).Returns(claimsPrincipal);
            mockContext.Setup(c => c.ConnectionId).Returns("conn1");

            var hub = new EcommerceHub(Mock.Of<IChatService>())
            {
                Clients = mockClients.Object,
                Groups = mockGroupManager.Object,
                Context = mockContext.Object
            };

            var options = CreateInMemoryOptions();
            using var dbContext = new ApplicationDbContext(options);

            // Act
            await hub.JoinOrderGroup(999, dbContext);

            // Assert
            mockGroupManager.Verify(g => g.AddToGroupAsync("conn1", "Order:999", default), Times.Once);
        }

        [Fact]
        public async Task JoinOrderGroup_Customer_CanJoinOwnOrder()
        {
            // Arrange
            var mockClients = new Mock<IHubCallerClients>();
            var mockGroupManager = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "200"),
                new Claim(ClaimTypes.Role, "Customer")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            mockContext.Setup(c => c.User).Returns(claimsPrincipal);
            mockContext.Setup(c => c.ConnectionId).Returns("conn2");

            var hub = new EcommerceHub(Mock.Of<IChatService>())
            {
                Clients = mockClients.Object,
                Groups = mockGroupManager.Object,
                Context = mockContext.Object
            };

            var options = CreateInMemoryOptions();
            using var dbContext = new ApplicationDbContext(options);
            
            dbContext.Orders.Add(new Order { Id = 10, UserId = 200 });
            await dbContext.SaveChangesAsync();

            // Act
            await hub.JoinOrderGroup(10, dbContext);

            // Assert
            mockGroupManager.Verify(g => g.AddToGroupAsync("conn2", "Order:10", default), Times.Once);
        }

        [Fact]
        public async Task JoinOrderGroup_Customer_CannotJoinOtherOrder_ThrowsException()
        {
            // Arrange
            var mockClients = new Mock<IHubCallerClients>();
            var mockGroupManager = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "200"),
                new Claim(ClaimTypes.Role, "Customer")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            mockContext.Setup(c => c.User).Returns(claimsPrincipal);
            mockContext.Setup(c => c.ConnectionId).Returns("conn3");

            var hub = new EcommerceHub(Mock.Of<IChatService>())
            {
                Clients = mockClients.Object,
                Groups = mockGroupManager.Object,
                Context = mockContext.Object
            };

            var options = CreateInMemoryOptions();
            using var dbContext = new ApplicationDbContext(options);
            
            dbContext.Orders.Add(new Order { Id = 10, UserId = 300 }); // Different user
            await dbContext.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<HubException>(() => hub.JoinOrderGroup(10, dbContext));
            mockGroupManager.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task JoinProductGroup_Anonymous_CanJoin()
        {
            var mockGroupManager = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("conn4");

            var hub = new EcommerceHub(Mock.Of<IChatService>())
            {
                Groups = mockGroupManager.Object,
                Context = mockContext.Object
            };

            await hub.JoinProductGroup(5);

            mockGroupManager.Verify(g => g.AddToGroupAsync("conn4", "Product:5", default), Times.Once);
        }

        [Fact]
        public async Task JoinOrderGroup_Anonymous_CannotJoin()
        {
            var mockGroupManager = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("conn5");
            // No User setup

            var hub = new EcommerceHub(Mock.Of<IChatService>())
            {
                Groups = mockGroupManager.Object,
                Context = mockContext.Object
            };

            var options = CreateInMemoryOptions();
            using var dbContext = new ApplicationDbContext(options);

            await Assert.ThrowsAsync<HubException>(() => hub.JoinOrderGroup(5, dbContext));
        }

        [Fact]
        public async Task JoinOrderGroup_InvalidId_Throws()
        {
            var hub = new EcommerceHub(Mock.Of<IChatService>());
            var options = CreateInMemoryOptions();
            using var dbContext = new ApplicationDbContext(options);

            await Assert.ThrowsAsync<HubException>(() => hub.JoinOrderGroup(0, dbContext));
            await Assert.ThrowsAsync<HubException>(() => hub.JoinOrderGroup(-1, dbContext));
        }

        [Fact]
        public async Task JoinProductGroup_InvalidId_Throws()
        {
            var hub = new EcommerceHub(Mock.Of<IChatService>());

            await Assert.ThrowsAsync<HubException>(() => hub.JoinProductGroup(0));
            await Assert.ThrowsAsync<HubException>(() => hub.JoinProductGroup(-1));
        }
    }
}
