using System.Threading.Tasks;

namespace Fruitables.Services.Communications
{
    public interface IRealtimeNotifier
    {
        Task NotifyOrderCreatedAsync(int orderId, int? userId);
        Task NotifyOrderUpdatedAsync(int orderId, int? userId, string newStatus);
        Task NotifyPaymentStatusChangedAsync(int orderId, int? userId, string newPaymentStatus);
        Task NotifyOrderNoteAddedAsync(int orderId, string noteSnippet);
        Task NotifyStockChangedAsync(int productId, decimal newStock);
        Task NotifyStockChangedAsync(int productId, decimal newStock, int? variantId);
        Task NotifyPriceChangedAsync(int productId, int? variantId = null);
        Task NotifySevereReviewAlertAsync(int reviewId, string productName, string commentSnippet);
    }
}
