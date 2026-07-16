using System.Threading.Tasks;

namespace Fruitables.Services.Interfaces
{
    public interface IRealtimeNotifier
    {
        Task NotifyOrderCreatedAsync(int orderId, int? userId);
        Task NotifyOrderUpdatedAsync(int orderId, int? userId, string newStatus);
        Task NotifyPaymentStatusChangedAsync(int orderId, int? userId, string newPaymentStatus);
        Task NotifyOrderNoteAddedAsync(int orderId, string noteSnippet);
        Task NotifyStockChangedAsync(int productId, int newStock);
        Task NotifyStockChangedAsync(int productId, int newStock, int? variantId);
        Task NotifyPriceChangedAsync(int productId, int? variantId = null);
    }
}
