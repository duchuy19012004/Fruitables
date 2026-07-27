using Fruitables.Models;
using Microsoft.AspNetCore.Http;

namespace Fruitables.ViewModels;

public class OrderListRequest
{
    public string? Search { get; set; }
    public OrderStatus? Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; }  // CreatedAt, Total, Status
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class OrderListResult
{
    public List<Order> Orders { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class UpdateOrderStatusRequest
{
    public int OrderId { get; set; }
    public OrderStatus NewStatus { get; set; }
    public int AdminId { get; set; }
    public string? Notes { get; set; }
}

public class UpdatePaymentStatusRequest
{
    public int OrderId { get; set; }
    public PaymentStatus NewPaymentStatus { get; set; }
    public int AdminId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for combined order status and payment status update
/// Supports file attachments for Cancelled/Returned orders (Requirements 1.1, 10.1)
/// </summary>
public class UpdateCombinedStatusRequest
{
    public int OrderId { get; set; }
    public OrderStatus NewOrderStatus { get; set; }
    public PaymentStatus NewPaymentStatus { get; set; }
    public string? Notes { get; set; }
    public List<IFormFile>? Attachments { get; set; }
    
    /// <summary>
    /// Admin ID performing the update. Will be moved to method parameter in future refactoring.
    /// </summary>
    public int AdminId { get; set; }
}

/// <summary>
/// Defines the rules for valid status combinations based on OrderStatus and PaymentMethod
/// Requirements: 2.1, 2.2, 2.3, 2.4, 7.1, 7.2, 7.3
/// </summary>
public class StatusCombinationRule
{
    public OrderStatus OrderStatus { get; set; }
    public PaymentStatus[] AllowedPaymentStatuses { get; set; } = Array.Empty<PaymentStatus>();
    public PaymentStatus? AutoSetPaymentStatus { get; set; }
    public bool IsPaymentLocked { get; set; }
}

/// <summary>
/// Defines the rules for valid state transitions based on current OrderStatus
/// Requirements: 6.1, 6.2, 6.3, 6.4, 6.5
/// </summary>
public class StateTransitionRule
{
    public OrderStatus CurrentStatus { get; set; }
    public OrderStatus[] AllowedTransitions { get; set; } = Array.Empty<OrderStatus>();
    public OrderStatus[] DisabledTransitions { get; set; } = Array.Empty<OrderStatus>();
}

/// <summary>
/// Static class containing all state transition rules
/// Requirements: 6.1, 6.2, 6.3, 6.4, 6.5
/// </summary>
public static class StateTransitionRules
{
    /// <summary>
    /// State transition rules for each OrderStatus
    /// - Pending: Can transition to Processing or Cancelled
    /// - Processing: Can transition to Shipped or Cancelled
    /// - Shipped: Can transition only to Delivered
    /// - Delivered: Terminal for the order lifecycle; post-delivery issues use ReturnRequest
    /// - Cancelled: Terminal state - no transitions allowed
    /// - Returned: Legacy terminal state kept only for historical records
    /// </summary>
    public static readonly Dictionary<OrderStatus, StateTransitionRule> Rules = new()
    {
        [OrderStatus.Pending] = new StateTransitionRule
        {
            CurrentStatus = OrderStatus.Pending,
            AllowedTransitions = new[] { OrderStatus.Processing, OrderStatus.Cancelled },
            DisabledTransitions = Array.Empty<OrderStatus>()
        },
        [OrderStatus.Processing] = new StateTransitionRule
        {
            CurrentStatus = OrderStatus.Processing,
            AllowedTransitions = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
            DisabledTransitions = new[] { OrderStatus.Pending }
        },
        [OrderStatus.Shipped] = new StateTransitionRule
        {
            CurrentStatus = OrderStatus.Shipped,
            AllowedTransitions = new[] { OrderStatus.Delivered },
            DisabledTransitions = new[] { OrderStatus.Pending, OrderStatus.Processing }
        },
        [OrderStatus.Delivered] = new StateTransitionRule
        {
            CurrentStatus = OrderStatus.Delivered,
            AllowedTransitions = Array.Empty<OrderStatus>(),
            DisabledTransitions = new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.Returned }
        },
        [OrderStatus.Cancelled] = new StateTransitionRule
        {
            CurrentStatus = OrderStatus.Cancelled,
            AllowedTransitions = Array.Empty<OrderStatus>(),
            DisabledTransitions = new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Returned }
        },
        [OrderStatus.Returned] = new StateTransitionRule
        {
            CurrentStatus = OrderStatus.Returned,
            AllowedTransitions = Array.Empty<OrderStatus>(),
            DisabledTransitions = new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled }
        }
    };

    /// <summary>
    /// Gets the transition rule for a specific order status
    /// </summary>
    /// <param name="currentStatus">The current order status</param>
    /// <returns>The applicable StateTransitionRule</returns>
    public static StateTransitionRule GetRule(OrderStatus currentStatus)
    {
        return Rules.TryGetValue(currentStatus, out var rule)
            ? rule
            : throw new ArgumentException($"No transition rule defined for OrderStatus: {currentStatus}");
    }

    /// <summary>
    /// Gets the allowed transitions for a specific order status
    /// </summary>
    /// <param name="currentStatus">The current order status</param>
    /// <returns>Array of allowed OrderStatus values to transition to</returns>
    public static OrderStatus[] GetAllowedTransitions(OrderStatus currentStatus)
    {
        return GetRule(currentStatus).AllowedTransitions;
    }

    /// <summary>
    /// Gets the disabled transitions for a specific order status
    /// </summary>
    /// <param name="currentStatus">The current order status</param>
    /// <returns>Array of disabled OrderStatus values</returns>
    public static OrderStatus[] GetDisabledTransitions(OrderStatus currentStatus)
    {
        return GetRule(currentStatus).DisabledTransitions;
    }

    /// <summary>
    /// Validates if a state transition is valid
    /// Requirements: 6.1, 6.2, 6.3, 6.4, 6.5
    /// </summary>
    /// <param name="currentStatus">The current order status</param>
    /// <param name="newStatus">The new order status to transition to</param>
    /// <returns>True if the transition is valid</returns>
    public static bool IsValidTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        // Same status is always valid (no change)
        if (currentStatus == newStatus)
            return true;

        var rule = GetRule(currentStatus);
        return rule.AllowedTransitions.Contains(newStatus);
    }

    /// <summary>
    /// Checks if a status is terminal in the order lifecycle.
    /// Delivered issues are handled by ReturnRequest instead of reopening the order.
    /// Requirements: 6.2, 6.3
    /// </summary>
    /// <param name="status">The order status to check</param>
    /// <returns>True if the status is terminal</returns>
    public static bool IsTerminalState(OrderStatus status)
    {
        return status is OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.Returned;
    }
}

/// <summary>
/// Static class containing all status combination rules with payment method awareness
/// Requirements: 2.1, 2.2, 2.3, 2.4, 7.1, 7.2, 7.3
/// </summary>
public static class StatusCombinationRules
{
    /// <summary>
    /// Rules for COD (Cash on Delivery) orders
    /// Requirement 7.1: COD orders have PaymentStatus locked to Pending for Pending/Processing/Shipped
    /// </summary>
    public static readonly Dictionary<OrderStatus, StatusCombinationRule> CodRules = new()
    {
        [OrderStatus.Pending] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Pending,
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending },
            AutoSetPaymentStatus = PaymentStatus.Pending,
            IsPaymentLocked = true
        },
        [OrderStatus.Processing] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Processing,
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending },
            AutoSetPaymentStatus = PaymentStatus.Pending,
            IsPaymentLocked = true
        },
        [OrderStatus.Shipped] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Shipped,
            // COD: shipper có thể thu tiền trước khi admin xác nhận Delivered
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending, PaymentStatus.Paid },
            AutoSetPaymentStatus = null,
            IsPaymentLocked = false
        },
        [OrderStatus.Delivered] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Delivered,
            AllowedPaymentStatuses = new[] { PaymentStatus.Paid },
            AutoSetPaymentStatus = PaymentStatus.Paid,
            IsPaymentLocked = true
        },
        [OrderStatus.Cancelled] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Cancelled,
            // A paid cancellation stays Paid until RefundService confirms money movement.
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending, PaymentStatus.Paid, PaymentStatus.Refunded },
            AutoSetPaymentStatus = null,
            IsPaymentLocked = false
        },
        [OrderStatus.Returned] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Returned,
            AllowedPaymentStatuses = new[] { PaymentStatus.Refunded },
            AutoSetPaymentStatus = PaymentStatus.Refunded,
            IsPaymentLocked = true
        }
    };

    /// <summary>
    /// Rules for Bank Transfer orders
    /// Requirement 7.2: Bank Transfer orders allow Paid before Delivered (prepayment)
    /// </summary>
    public static readonly Dictionary<OrderStatus, StatusCombinationRule> BankTransferRules = new()
    {
        [OrderStatus.Pending] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Pending,
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending, PaymentStatus.Paid },
            AutoSetPaymentStatus = null,
            IsPaymentLocked = false
        },
        [OrderStatus.Processing] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Processing,
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending, PaymentStatus.Paid },
            AutoSetPaymentStatus = null,
            IsPaymentLocked = false
        },
        [OrderStatus.Shipped] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Shipped,
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending, PaymentStatus.Paid },
            AutoSetPaymentStatus = null,
            IsPaymentLocked = false
        },
        [OrderStatus.Delivered] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Delivered,
            AllowedPaymentStatuses = new[] { PaymentStatus.Paid },
            AutoSetPaymentStatus = PaymentStatus.Paid,
            IsPaymentLocked = true
        },
        [OrderStatus.Cancelled] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Cancelled,
            // A paid cancellation stays Paid until RefundService confirms money movement.
            AllowedPaymentStatuses = new[] { PaymentStatus.Pending, PaymentStatus.Paid, PaymentStatus.Refunded },
            AutoSetPaymentStatus = null,
            IsPaymentLocked = false
        },
        [OrderStatus.Returned] = new StatusCombinationRule
        {
            OrderStatus = OrderStatus.Returned,
            AllowedPaymentStatuses = new[] { PaymentStatus.Refunded },
            AutoSetPaymentStatus = PaymentStatus.Refunded,
            IsPaymentLocked = true
        }
    };

    /// <summary>
    /// Legacy rules dictionary for backward compatibility (uses COD rules as default)
    /// </summary>
    public static readonly Dictionary<OrderStatus, StatusCombinationRule> Rules = CodRules;

    /// <summary>
    /// Gets the combination rule for a specific order status and payment method
    /// Requirements: 7.1, 7.2, 7.3
    /// </summary>
    /// <param name="orderStatus">The order status</param>
    /// <param name="paymentMethod">The payment method (COD, BankTransfer, etc.)</param>
    /// <returns>The applicable StatusCombinationRule</returns>
    public static StatusCombinationRule GetRule(OrderStatus orderStatus, PaymentMethod paymentMethod)
    {
        var rules = IsCodPaymentMethod(paymentMethod) ? CodRules : BankTransferRules;
        
        return rules.TryGetValue(orderStatus, out var rule) 
            ? rule 
            : throw new ArgumentException($"No rule defined for OrderStatus: {orderStatus}");
    }

    /// <summary>
    /// Gets the combination rule for a specific order status (legacy method, uses COD rules)
    /// </summary>
    public static StatusCombinationRule GetRule(OrderStatus orderStatus)
    {
        return GetRule(orderStatus, PaymentMethod.COD);
    }

    /// <summary>
    /// Gets the allowed payment statuses for a specific order status and payment method
    /// </summary>
    public static PaymentStatus[] GetAllowedPaymentStatuses(OrderStatus orderStatus, PaymentMethod paymentMethod)
    {
        return GetRule(orderStatus, paymentMethod).AllowedPaymentStatuses;
    }

    /// <summary>
    /// Gets the allowed payment statuses for a specific order status (legacy method)
    /// </summary>
    public static PaymentStatus[] GetAllowedPaymentStatuses(OrderStatus orderStatus)
    {
        return GetRule(orderStatus).AllowedPaymentStatuses;
    }

    /// <summary>
    /// Validates if a status combination is valid for a given payment method
    /// Requirements: 3.2, 3.3
    /// </summary>
    /// <param name="orderStatus">The order status</param>
    /// <param name="paymentStatus">The payment status</param>
    /// <param name="paymentMethod">The payment method</param>
    /// <returns>True if the combination is valid</returns>
    public static bool IsValidCombination(OrderStatus orderStatus, PaymentStatus paymentStatus, PaymentMethod paymentMethod)
    {
        var rules = IsCodPaymentMethod(paymentMethod) ? CodRules : BankTransferRules;
        
        if (!rules.TryGetValue(orderStatus, out var rule))
            return false;

        return rule.AllowedPaymentStatuses.Contains(paymentStatus);
    }

    /// <summary>
    /// Validates if a status combination is valid (legacy method, uses COD rules)
    /// </summary>
    public static bool IsValidCombination(OrderStatus orderStatus, PaymentStatus paymentStatus)
    {
        return IsValidCombination(orderStatus, paymentStatus, PaymentMethod.COD);
    }

    /// <summary>
    /// Determines if a payment method should use COD rules
    /// COD is the only payment method that requires payment at delivery
    /// All other methods (BankTransfer, Check, Paypal) allow prepayment
    /// </summary>
    private static bool IsCodPaymentMethod(PaymentMethod paymentMethod)
    {
        return paymentMethod == PaymentMethod.COD;
    }
}

public class AddOrderNoteRequest
{
    public string Content { get; set; } = string.Empty;
}
