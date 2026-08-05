using Fruitables.Models;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;

namespace Fruitables.Services.Orders.OrderManagement;

public interface IOrderAdminService
{
    // Order List & Detail
    Task<OrderListResult> GetOrdersAsync(OrderListRequest request);
    Task<Order?> GetOrderByIdAsync(int id);
    Task<Order?> GetOrderWithHistoryAsync(int id);

    // Status Management
    Task<OrderResult> UpdateOrderStatusAsync(UpdateOrderStatusRequest request);
    Task<OrderResult> UpdatePaymentStatusAsync(UpdatePaymentStatusRequest request);
    
    // Combined Status Update with Audit Trail
    // Requirements: 1.2 - Updates both OrderStatus and PaymentStatus in a single transaction
    Task<OrderResult> UpdateCombinedStatusAsync(UpdateCombinedStatusRequest request, int adminId);

    // Validation helpers
    bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus);
    bool IsValidPaymentStatusTransition(PaymentStatus currentStatus, PaymentStatus newStatus, OrderStatus orderStatus);
    
    // State transition validation (Requirements: 6.1, 6.2, 6.3, 6.4, 6.5)
    bool IsValidStateTransition(OrderStatus currentStatus, OrderStatus newStatus);
    StateTransitionRule GetStateTransitionRule(OrderStatus currentStatus);
    OrderStatus[] GetAllowedTransitions(OrderStatus currentStatus);
    
    // Status combination validation (legacy - uses COD rules)
    bool IsValidStatusCombination(OrderStatus orderStatus, PaymentStatus paymentStatus);
    StatusCombinationRule GetStatusCombinationRule(OrderStatus orderStatus);
    PaymentStatus[] GetAllowedPaymentStatuses(OrderStatus orderStatus);
    
    // Status combination validation (payment method aware)
    // Requirements: 3.2, 3.3, 7.1, 7.2, 7.3
    bool IsValidStatusCombination(OrderStatus orderStatus, PaymentStatus paymentStatus, PaymentMethod paymentMethod);
    StatusCombinationRule GetStatusCombinationRule(OrderStatus orderStatus, PaymentMethod paymentMethod);
    PaymentStatus[] GetAllowedPaymentStatuses(OrderStatus orderStatus, PaymentMethod paymentMethod);
    
    // Order Notes (Internal comments by admin)
    Task<List<OrderNote>> GetOrderNotesAsync(int orderId);
    Task<OrderNote> AddOrderNoteAsync(int orderId, string content, int adminId, string adminName);
    Task<bool> DeleteOrderNoteAsync(int noteId, int adminId);
}
