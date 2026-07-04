using Xunit;
using Moq;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fruitables.Tests
{
    public class OrderServiceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task CreateOrderAsync_MissingProduct_ThrowsInvalidOperationException()
        {
            // Arrange
            var options = CreateInMemoryOptions();
            using var context = new ApplicationDbContext(options);
            var unitOfWork = new UnitOfWork(context);

            var cartServiceMock = new Mock<ICartService>();
            var sessionId = "test-session";

            var cart = new CartViewModel
            {
                Items = new List<CartItemViewModel>
                {
                    new CartItemViewModel { ProductId = 999, ProductName = "Product 999", Price = 10, Quantity = 1 }
                },
                Subtotal = 10,
                ShippingFee = 15,
                Total = 25
            };

            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(cart);

            var notifierMock = new Mock<IRealtimeNotifier>();
            var orderService = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);
            var checkoutModel = new CheckoutViewModel
            {
                FirstName = "Test",
                StreetAddress = "123 Main St",
                Mobile = "0123456789",
                ProvinceCode = "1",
                CommuneCode = "1",
                PaymentMethod = PaymentMethod.COD,
                ShippingMethod = ShippingMethod.FlatRate
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                orderService.CreateOrderAsync(checkoutModel, sessionId));

            Assert.Contains("Một số sản phẩm không tồn tại", exception.Message);
        }

        [Fact]
        public async Task CreateOrderAsync_LowStock_ThrowsInvalidOperationException()
        {
            // Arrange
            var options = CreateInMemoryOptions();
            using var context = new ApplicationDbContext(options);

            var product = new Product
            {
                Id = 1,
                Name = "Apple",
                Slug = "apple",
                Price = 10,
                StockQuantity = 2,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var unitOfWork = new UnitOfWork(context);
            var cartServiceMock = new Mock<ICartService>();
            var sessionId = "test-session";

            var cart = new CartViewModel
            {
                Items = new List<CartItemViewModel>
                {
                    new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10, Quantity = 5 }
                },
                Subtotal = 50,
                ShippingFee = 15,
                Total = 65
            };

            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(cart);

            var notifierMock = new Mock<IRealtimeNotifier>();
            var orderService = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);
            var checkoutModel = new CheckoutViewModel
            {
                FirstName = "Test",
                StreetAddress = "123 Main St",
                Mobile = "0123456789",
                ProvinceCode = "1",
                CommuneCode = "1",
                PaymentMethod = PaymentMethod.COD,
                ShippingMethod = ShippingMethod.FlatRate
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                orderService.CreateOrderAsync(checkoutModel, sessionId));

            Assert.Contains("không đủ số lượng tồn kho", exception.Message);
        }

        [Fact]
        public async Task CreateOrderAsync_DuplicateProductLines_ValidatesTotalQuantity()
        {
            // Two cart lines pointing to the same product. The validator must sum the quantities
            // (3 + 3 = 6) and reject the order against stock 5.
            var options = CreateInMemoryOptions();
            using var context = new ApplicationDbContext(options);

            context.Products.Add(new Product
            {
                Id = 1,
                Name = "Apple",
                Slug = "apple",
                Price = 10,
                StockQuantity = 5,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var unitOfWork = new UnitOfWork(context);
            var cartServiceMock = new Mock<ICartService>();
            var sessionId = "dup-session";
            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(
                new CartViewModel
                {
                    Items = new List<CartItemViewModel>
                    {
                        new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10, Quantity = 3 },
                        new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10, Quantity = 3 }
                    },
                    Subtotal = 60, ShippingFee = 15, Total = 75
                });

            var notifierMock = new Mock<IRealtimeNotifier>();
            var orderService = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);
            var checkoutModel = new CheckoutViewModel
            {
                FirstName = "Test", StreetAddress = "123 St", Mobile = "0123456789",
                ProvinceCode = "1", CommuneCode = "1",
                PaymentMethod = PaymentMethod.COD, ShippingMethod = ShippingMethod.FlatRate
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                orderService.CreateOrderAsync(checkoutModel, sessionId));
            Assert.Contains("không đủ số lượng tồn kho", ex.Message);

            Assert.False(await context.Orders.AnyAsync());
            var product = await context.Products.AsNoTracking().FirstAsync(p => p.Id == 1);
            Assert.Equal(5, product.StockQuantity);
        }

        [Fact]
        public async Task CreateOrderAsync_Success_AtomicOnSqlite()
        {
            // Verify on SQLite: order + address + stock are all committed in a single save inside one transaction.
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);

            // Seed a customer user (avoids FK violations on User references and is independent from seeded admin).
            context.Users.Add(new User
            {
                Id = 100,
                Name = "Test Buyer",
                Email = "buyer@example.com",
                Password = "hashed_password",
                Role = UserRole.Customer,
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
                StockQuantity = 10,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var unitOfWork = new UnitOfWork(context);
            var cartServiceMock = new Mock<ICartService>();
            var sessionId = "atomic-session";

            var cart = new CartViewModel
            {
                Items = new List<CartItemViewModel>
                {
                    new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10, Quantity = 3 }
                },
                Subtotal = 30,
                ShippingFee = 15,
                Total = 45
            };

            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(cart);

            var notifierMock = new Mock<IRealtimeNotifier>();
            var orderService = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);
            var checkoutModel = new CheckoutViewModel
            {
                FirstName = "Test",
                StreetAddress = "123 Main St",
                Mobile = "0123456789",
                ProvinceCode = "1",
                ProvinceName = "Hanoi",
                CommuneCode = "1",
                CommuneName = "Phuc Xa",
                PaymentMethod = PaymentMethod.COD,
                ShippingMethod = ShippingMethod.FlatRate
            };

            // Act
            var order = await orderService.CreateOrderAsync(checkoutModel, sessionId, userId: 100);

            // Assert
            Assert.NotNull(order);
            Assert.Equal(OrderStatus.Pending, order.Status);

            // ExecuteUpdateAsync bypasses the change tracker — the tracked product still has the
            // old StockQuantity. Clear the tracker so FindAsync hits the database.
            context.ChangeTracker.Clear();
            var updatedProduct = await context.Products.FindAsync(1);
            Assert.NotNull(updatedProduct);
            Assert.Equal(7, updatedProduct!.StockQuantity);

            Assert.True(order.AddressId > 0);
            var address = await context.Addresses.FindAsync(order.AddressId);
            Assert.NotNull(address);
            Assert.Equal("Test", address!.FullName);
            Assert.Equal("1", address.CommuneCode);
            Assert.Equal("Phuc Xa", address.CommuneName);

            cartServiceMock.Verify(c => c.ClearCartAsync(sessionId), Times.Once);
            notifierMock.Verify(n => n.NotifyOrderCreatedAsync(order.Id, 100), Times.Once);
            notifierMock.Verify(n => n.NotifyStockChangedAsync(1, 7), Times.Once);
        }

        [Fact]
        public async Task CreateOrderAsync_DepletedStockBeforeValidation_Throws()
        {
            // Stock was depleted by another request before CreateOrderAsync loaded the product.
            // Initial validation catches the shortage, so the order is rejected without ever
            // reaching the conditional update path.
            var options = TestDbContextFactory.CreateSqliteOptions();
            using var context = new ApplicationDbContext(options);

            context.Categories.Add(new Category { Id = 1, Name = "Default", Slug = "default" });
            context.Users.Add(new User
            {
                Id = 100, Name = "Buyer", Email = "buyer@test.com", Password = "pw",
                Role = UserRole.Customer, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            context.Products.Add(new Product
            {
                Id = 1, CategoryId = 1, Name = "Apple", Slug = "apple",
                Price = 10, StockQuantity = 5, MinOrderQuantity = 1, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Simulate concurrent request: deduct all stock via a separate context.
            using (var other = new ApplicationDbContext(options))
            {
                await other.Products
                    .Where(p => p.Id == 1)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 0));
            }

            // Reset tracker — the first context still sees the old stock value.
            context.ChangeTracker.Clear();

            var unitOfWork = new UnitOfWork(context);
            var cartServiceMock = new Mock<ICartService>();
            var sessionId = "stale-session";
            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(
                new CartViewModel
                {
                    Items = new List<CartItemViewModel>
                    {
                        new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10, Quantity = 3 }
                    },
                    Subtotal = 30, ShippingFee = 15, Total = 45
                });

            var notifierMock = new Mock<IRealtimeNotifier>();
            var orderService = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);
            var checkoutModel = new CheckoutViewModel
            {
                FirstName = "Test", StreetAddress = "123 St", Mobile = "0123456789",
                ProvinceCode = "1", CommuneCode = "1",
                PaymentMethod = PaymentMethod.COD, ShippingMethod = ShippingMethod.FlatRate
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                orderService.CreateOrderAsync(checkoutModel, sessionId, userId: 100));
            Assert.Contains("không đủ số lượng tồn kho", ex.Message);

            // Confirm no order was created.
            Assert.False(await context.Orders.AnyAsync());
            // Stock unchanged (still 0 from concurrent deduct).
            var product = await context.Products.AsNoTracking().FirstAsync(p => p.Id == 1);
            Assert.Equal(0, product.StockQuantity);

            notifierMock.Verify(n => n.NotifyOrderCreatedAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
            notifierMock.Verify(n => n.NotifyStockChangedAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task CreateOrderAsync_ProductLookup_UsesSingleProductQuery()
        {
            // Use SQLite + query interceptor to verify the batch product lookup issues exactly one
            // SELECT against Products, not N+1 per-item queries.
            var interceptor = new CountingQueryInterceptor();
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .AddInterceptors(interceptor)
                .Options;

            using (var seedCtx = new ApplicationDbContext(options))
            {
                seedCtx.Database.EnsureCreated();
                seedCtx.Users.Add(new User
                {
                    Id = 100,
                    Name = "Bulk Buyer",
                    Email = "bulk@example.com",
                    Password = "hashed",
                    Role = UserRole.Customer,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                seedCtx.Categories.Add(new Category { Id = 1, Name = "Default", Slug = "default" });
                for (int i = 1; i <= 20; i++)
                {
                    seedCtx.Products.Add(new Product
                    {
                        Id = i,
                        CategoryId = 1,
                        Name = $"P{i}",
                        Slug = $"p{i}",
                        Price = 10,
                        StockQuantity = 100,
                        MinOrderQuantity = 1,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await seedCtx.SaveChangesAsync();
            }

            using var context = new ApplicationDbContext(options);
            var unitOfWork = new UnitOfWork(context);
            var cartServiceMock = new Mock<ICartService>();
            var sessionId = "n1-session";

            var cart = new CartViewModel
            {
                Items = Enumerable.Range(1, 20)
                    .Select(i => new CartItemViewModel { ProductId = i, ProductName = $"P{i}", Price = 10, Quantity = 1 })
                    .ToList(),
                Subtotal = 200,
                ShippingFee = 15,
                Total = 215
            };
            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(cart);

            var notifierMock = new Mock<IRealtimeNotifier>();
            var orderService = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);
            var checkoutModel = new CheckoutViewModel
            {
                FirstName = "Bulk",
                StreetAddress = "123 Bulk",
                Mobile = "0123456789",
                ProvinceCode = "1",
                CommuneCode = "1",
                PaymentMethod = PaymentMethod.COD,
                ShippingMethod = ShippingMethod.FlatRate
            };

            var order = await orderService.CreateOrderAsync(checkoutModel, sessionId, userId: 100);

            Assert.NotNull(order);
            Assert.Equal(20, order.Items.Count);

            // Verify a single product query (not N+1).
            // EnsureCreated + sqlite_master queries inflate total SELECT count, but
            // product-specific SELECT must be exactly 1 (the batch lookup).
            Assert.Equal(1, interceptor.ProductSelectCount);

            // Stock deducted for all 20 items. Clear tracker because ExecuteUpdateAsync bypasses it.
            context.ChangeTracker.Clear();
            var stocks = Enumerable.Range(1, 20)
                .Select(i => context.Products.Find(i)!.StockQuantity)
                .ToList();
            Assert.All(stocks, s => Assert.Equal(99, s));
        }

        [Fact]
        public async Task CreateOrderAsync_BankTransfer_GeneratesPaymentCode()
        {
            var options = CreateInMemoryOptions();
            using var context = new ApplicationDbContext(options);

            context.Products.Add(new Product
            {
                Id = 1,
                Name = "Apple",
                Slug = "apple",
                Price = 10,
                StockQuantity = 10,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var unitOfWork = new UnitOfWork(context);
            var cartServiceMock = new Mock<ICartService>();
            const string sessionId = "sepay-bank-transfer";
            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(new CartViewModel
            {
                Items = new List<CartItemViewModel>
                {
                    new() { ProductId = 1, ProductName = "Apple", Price = 10, Quantity = 1 }
                },
                Subtotal = 10,
                ShippingFee = 15,
                Total = 25
            });

            var notifierMock = new Mock<IRealtimeNotifier>();
            var service = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);

            var order = await service.CreateOrderAsync(new CheckoutViewModel
            {
                FirstName = "Test",
                StreetAddress = "123 Main",
                Mobile = "0123456789",
                ProvinceCode = "1",
                CommuneCode = "1",
                PaymentMethod = PaymentMethod.BankTransfer,
                ShippingMethod = ShippingMethod.FlatRate
            }, sessionId);

            Assert.Matches("^FTB[A-Z0-9]{8}$", order.PaymentCode);
        }

        [Fact]
        public async Task CreateOrderAsync_Cod_DoesNotGeneratePaymentCode()
        {
            var options = CreateInMemoryOptions();
            using var context = new ApplicationDbContext(options);

            context.Products.Add(new Product
            {
                Id = 1,
                Name = "Apple",
                Slug = "apple",
                Price = 10,
                StockQuantity = 10,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var unitOfWork = new UnitOfWork(context);
            var cartServiceMock = new Mock<ICartService>();
            const string sessionId = "sepay-cod";
            cartServiceMock.Setup(c => c.GetCartAsync(sessionId, null)).ReturnsAsync(new CartViewModel
            {
                Items = new List<CartItemViewModel>
                {
                    new() { ProductId = 1, ProductName = "Apple", Price = 10, Quantity = 1 }
                },
                Subtotal = 10,
                ShippingFee = 15,
                Total = 25
            });

            var notifierMock = new Mock<IRealtimeNotifier>();
            var service = new OrderService(unitOfWork, cartServiceMock.Object, notifierMock.Object);

            var order = await service.CreateOrderAsync(new CheckoutViewModel
            {
                FirstName = "Test",
                StreetAddress = "123 Main",
                Mobile = "0123456789",
                ProvinceCode = "1",
                CommuneCode = "1",
                PaymentMethod = PaymentMethod.COD,
                ShippingMethod = ShippingMethod.FlatRate
            }, sessionId);

            Assert.Null(order.PaymentCode);
        }
    }
}
