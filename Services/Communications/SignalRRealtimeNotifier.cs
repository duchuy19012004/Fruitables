using Fruitables.Hubs;
using Fruitables.Services.Communications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Fruitables.Services.Communications
{
    public class SignalRRealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<EcommerceHub> _hubContext;
        private readonly ILogger<SignalRRealtimeNotifier> _logger;

        public SignalRRealtimeNotifier(IHubContext<EcommerceHub> hubContext, ILogger<SignalRRealtimeNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyOrderCreatedAsync(int orderId, int? userId)
        {
            try
            {
                await _hubContext.Clients.Group("Admins").SendAsync("OrderCreated", new { OrderId = orderId });
                if (userId.HasValue)
                {
                    await _hubContext.Clients.Group($"User:{userId.Value}").SendAsync("OrderUpdated", new { OrderId = orderId, Status = "Pending" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting OrderCreated for OrderId: {OrderId}", orderId);
            }
        }

        public async Task NotifyOrderUpdatedAsync(int orderId, int? userId, string newStatus)
        {
            try
            {
                var payload = new { OrderId = orderId, Status = newStatus };
                await _hubContext.Clients.Group($"Order:{orderId}").SendAsync("OrderStatusChanged", payload);
                if (userId.HasValue)
                {
                    await _hubContext.Clients.Group($"User:{userId.Value}").SendAsync("OrderUpdated", payload);
                }
                await _hubContext.Clients.Group("Admins").SendAsync("OrderStatusChanged", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting OrderStatusChanged for OrderId: {OrderId}", orderId);
            }
        }

        public async Task NotifyPaymentStatusChangedAsync(int orderId, int? userId, string newPaymentStatus)
        {
            try
            {
                var payload = new { OrderId = orderId, PaymentStatus = newPaymentStatus };
                await _hubContext.Clients.Group($"Order:{orderId}").SendAsync("PaymentStatusChanged", payload);
                if (userId.HasValue)
                {
                    await _hubContext.Clients.Group($"User:{userId.Value}").SendAsync("OrderUpdated", payload);
                }
                await _hubContext.Clients.Group("Admins").SendAsync("PaymentStatusChanged", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting PaymentStatusChanged for OrderId: {OrderId}", orderId);
            }
        }

        public async Task NotifyOrderNoteAddedAsync(int orderId, string noteSnippet)
        {
            try
            {
                var payload = new { OrderId = orderId, Note = noteSnippet };
                await _hubContext.Clients.Group($"Order:{orderId}").SendAsync("OrderNoteAdded", payload);
                await _hubContext.Clients.Group("Admins").SendAsync("OrderNoteAdded", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting OrderNoteAdded for OrderId: {OrderId}", orderId);
            }
        }

        public Task NotifyStockChangedAsync(int productId, int newStock) => NotifyStockChangedAsync(productId, newStock, null);

        public async Task NotifyStockChangedAsync(int productId, int newStock, int? variantId)
        {
            try
            {
                await _hubContext.Clients.Group($"Product:{productId}").SendAsync("StockChanged", new { ProductId = productId, VariantId = variantId, Stock = newStock });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting StockChanged for ProductId: {ProductId}", productId);
            }
        }

        public async Task NotifyPriceChangedAsync(int productId, int? variantId = null)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("PriceChanged", new { ProductId = productId, VariantId = variantId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting PriceChanged for ProductId: {ProductId}", productId);
            }
        }

        public async Task NotifySevereReviewAlertAsync(int reviewId, string productName, string commentSnippet)
        {
            try
            {
                await _hubContext.Clients.Group("Admins").SendAsync("SevereReviewAlert",
                    new { ReviewId = reviewId, ProductName = productName, CommentSnippet = commentSnippet });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting SevereReviewAlert for ReviewId: {ReviewId}", reviewId);
            }
        }
    }
}
