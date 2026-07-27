using Xunit;
using Moq;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using System.Reflection;

namespace Fruitables.Tests
{
    public class OrderAdminServiceTests
    {
        [Fact]
        public async Task UpdateOrderStatusAsync_CancelOrder_AtomicStatusStockAndHistory()
        {
            // Arrange
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);

            context.Users.Add(new User
            {
                Id = 100,
                Name = "Test Admin",
                Email = "admin@example.com",
                Password = "hashed",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Categories.Add(new Category { Id = 1, Name = "Default", Slug = "default" });

            var product = new Product
            {
                Id = 1,
                CategoryId = 1,
                Name = "Apple",
                Slug = "apple",
                Price = 10,
                StockQuantity = 5,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var order = new Order
            {
                Id = 10,
                OrderNumber = "ORD-12345",
                Status = OrderStatus.Processing,
                Subtotal = 20,
                Total = 20,
                PaymentMethod = PaymentMethod.COD,
                PaymentStatus = PaymentStatus.Pending,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, ProductName = "Apple", Quantity = 2, Price = 10, Total = 20 }
                }
            };

            context.Products.Add(product);
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var logServiceMock = new Mock<IOrderLogService>();
            var notifierMock = new Mock<IRealtimeNotifier>();
            var adminService = new OrderAdminService(context, logServiceMock.Object, notifierMock.Object);

            // Act
            var result = await adminService.UpdateOrderStatusAsync(new UpdateOrderStatusRequest
            {
                OrderId = 10,
                NewStatus = OrderStatus.Cancelled,
                AdminId = 100,
                Notes = "Customer cancelled"
            });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(OrderStatus.Cancelled, order.Status);

            // Stock restored atomically (5 + 2 = 7).
            var updatedProduct = await context.Products.FindAsync(1);
            Assert.NotNull(updatedProduct);
            Assert.Equal(7, updatedProduct.StockQuantity);

            // History row was created inline (no separate log service save).
            var history = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(context.OrderStatusHistories, h => h.OrderId == 10);
            Assert.NotNull(history);
            Assert.Equal(OrderStatus.Processing, history!.OldStatus);
            Assert.Equal(OrderStatus.Cancelled, history.NewStatus);

            // Log service is no longer required for status change (kept only for payment status log).
            logServiceMock.Verify(
                s => s.LogStatusChangeAsync(It.IsAny<int>(), It.IsAny<OrderStatus>(), It.IsAny<OrderStatus>(), It.IsAny<int>(), It.IsAny<string?>()),
                Times.Never);

            notifierMock.Verify(n => n.NotifyOrderUpdatedAsync(10, order.UserId, "Cancelled"), Times.Once);
            notifierMock.Verify(n => n.NotifyStockChangedAsync(1, 7), Times.Once);
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_PendingToCancelled_RestoresProductStock()
        {
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);
            SeedAdminAndCategory(context);

            var product = CreateProduct(stockQuantity: 2);
            var order = CreateOrder(
                id: 11,
                orderNumber: "ORD-PENDING-CANCEL",
                status: OrderStatus.Pending,
                paymentStatus: PaymentStatus.Pending,
                productId: product.Id,
                quantity: 5);

            context.AddRange(product, order);
            await context.SaveChangesAsync();

            var notifierMock = new Mock<IRealtimeNotifier>();
            var adminService = new OrderAdminService(context, Mock.Of<IOrderLogService>(), notifierMock.Object);

            var result = await adminService.UpdateOrderStatusAsync(new UpdateOrderStatusRequest
            {
                OrderId = order.Id,
                NewStatus = OrderStatus.Cancelled,
                AdminId = 100,
                Notes = "Khách hủy trước khi xử lý"
            });

            Assert.True(result.Success);
            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(7, (await context.Products.FindAsync(product.Id))!.StockQuantity);
            notifierMock.Verify(n => n.NotifyStockChangedAsync(product.Id, 7), Times.Once);
        }

        [Theory]
        [InlineData(OrderStatus.Shipped)]
        [InlineData(OrderStatus.Delivered)]
        public async Task UpdateOrderStatusAsync_PostDispatchToCancelled_IsRejectedWithoutRestoringProductStock(
            OrderStatus currentStatus)
        {
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);
            SeedAdminAndCategory(context);

            var product = CreateProduct(stockQuantity: 2);
            var order = CreateOrder(
                id: 12,
                orderNumber: $"ORD-{currentStatus.ToString().ToUpperInvariant()}-CANCEL",
                status: currentStatus,
                paymentStatus: PaymentStatus.Paid,
                productId: product.Id,
                quantity: 5);

            context.AddRange(product, order);
            await context.SaveChangesAsync();

            var notifierMock = new Mock<IRealtimeNotifier>();
            var adminService = new OrderAdminService(context, Mock.Of<IOrderLogService>(), notifierMock.Object);

            var result = await adminService.UpdateOrderStatusAsync(new UpdateOrderStatusRequest
            {
                OrderId = order.Id,
                NewStatus = OrderStatus.Cancelled,
                AdminId = 100,
                Notes = "Không được hủy sau giao hàng"
            });

            Assert.False(result.Success);
            Assert.Equal(OrderErrorType.InvalidStatusTransition, result.ErrorType);
            Assert.Contains("Khiếu nại", result.ErrorMessage);
            Assert.Equal(currentStatus, order.Status);
            Assert.Equal(2, (await context.Products.FindAsync(product.Id))!.StockQuantity);
            Assert.Empty(context.OrderStatusHistories);
            notifierMock.Verify(n => n.NotifyOrderUpdatedAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);
            notifierMock.Verify(n => n.NotifyStockChangedAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateCombinedStatusAsync_DeliveredToReturned_IsRejectedWithoutRestoringProductStock()
        {
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);
            SeedAdminAndCategory(context);

            var product = CreateProduct(stockQuantity: 3);
            var order = CreateOrder(
                id: 13,
                orderNumber: "ORD-LEGACY-RETURN",
                status: OrderStatus.Delivered,
                paymentStatus: PaymentStatus.Paid,
                productId: product.Id,
                quantity: 2);

            context.AddRange(product, order);
            await context.SaveChangesAsync();

            var adminService = new OrderAdminService(
                context,
                Mock.Of<IOrderLogService>(),
                Mock.Of<IRealtimeNotifier>());

            var result = await adminService.UpdateCombinedStatusAsync(new UpdateCombinedStatusRequest
            {
                OrderId = order.Id,
                NewOrderStatus = OrderStatus.Returned,
                NewPaymentStatus = PaymentStatus.Refunded,
                Notes = "Legacy return must be blocked"
            }, 100);

            Assert.False(result.Success);
            Assert.Equal(OrderErrorType.InvalidStatusTransition, result.ErrorType);
            Assert.Contains("Khiếu nại", result.ErrorMessage);
            Assert.Equal(OrderStatus.Delivered, order.Status);
            Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
            Assert.Equal(3, (await context.Products.FindAsync(product.Id))!.StockQuantity);
        }

        [Fact]
        public async Task UpdatePaymentStatusAsync_PaidToRefunded_IsRejectedOutsideRefundModule()
        {
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);
            SeedAdminAndCategory(context);

            var product = CreateProduct(stockQuantity: 3);
            var order = CreateOrder(
                id: 14,
                orderNumber: "ORD-DIRECT-REFUND",
                status: OrderStatus.Cancelled,
                paymentStatus: PaymentStatus.Paid,
                productId: product.Id,
                quantity: 1);

            context.AddRange(product, order);
            await context.SaveChangesAsync();

            var logServiceMock = new Mock<IOrderLogService>();
            var adminService = new OrderAdminService(context, logServiceMock.Object, Mock.Of<IRealtimeNotifier>());

            var result = await adminService.UpdatePaymentStatusAsync(new UpdatePaymentStatusRequest
            {
                OrderId = order.Id,
                NewPaymentStatus = PaymentStatus.Refunded,
                AdminId = 100,
                Notes = "Direct refund is forbidden"
            });

            Assert.False(result.Success);
            Assert.Equal(OrderErrorType.InvalidPaymentStatusTransition, result.ErrorType);
            Assert.Contains("hoàn tiền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
            logServiceMock.Verify(
                service => service.LogPaymentStatusChangeAsync(
                    It.IsAny<int>(),
                    It.IsAny<PaymentStatus>(),
                    It.IsAny<PaymentStatus>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>()),
                Times.Never);
        }

        private static void SeedAdminAndCategory(ApplicationDbContext context)
        {
            context.Users.Add(new User
            {
                Id = 100,
                Name = "Test Admin",
                Email = $"admin-{Guid.NewGuid():N}@example.com",
                Password = "hashed",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Categories.Add(new Category { Id = 1, Name = "Default", Slug = "default" });
        }

        private static Product CreateProduct(int stockQuantity)
        {
            return new Product
            {
                Id = 1,
                CategoryId = 1,
                Name = "Apple",
                Slug = "apple",
                Price = 10,
                StockQuantity = stockQuantity,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static Order CreateOrder(
            int id,
            string orderNumber,
            OrderStatus status,
            PaymentStatus paymentStatus,
            int productId,
            int quantity)
        {
            return new Order
            {
                Id = id,
                OrderNumber = orderNumber,
                Status = status,
                Subtotal = 10m * quantity,
                Total = 10m * quantity,
                PaymentMethod = PaymentMethod.COD,
                PaymentStatus = paymentStatus,
                Items = new List<OrderItem>
                {
                    new()
                    {
                        ProductId = productId,
                        ProductName = "Apple",
                        Quantity = quantity,
                        Price = 10,
                        Total = 10m * quantity
                    }
                }
            };
        }
    }
}
