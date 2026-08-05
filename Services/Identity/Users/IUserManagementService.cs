using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Identity.Users;

/// <summary>
/// Interface for User Management Service
/// Provides functionality for managing customers in the admin panel
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Get list of customers with filtering, pagination, and sorting
    /// </summary>
    Task<UserListResult> GetCustomersAsync(UserFilterRequest filter);

    /// <summary>
    /// Get detailed information about a specific customer
    /// </summary>
    Task<UserManagementResult<CustomerDetailViewModel>> GetCustomerDetailAsync(int customerId);

    /// <summary>
    /// Get purchase history for a customer
    /// </summary>
    Task<UserManagementResult<CustomerPurchaseHistoryViewModel>> GetPurchaseHistoryAsync(
        int customerId,
        OrderStatus? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 10);

    /// <summary>
    /// Lock a customer account
    /// </summary>
    Task<UserManagementResult<LockAccountResultViewModel>> LockAccountAsync(LockAccountRequest request);

    /// <summary>
    /// Unlock a customer account
    /// </summary>
    Task<UserManagementResult<bool>> UnlockAccountAsync(UnlockAccountRequest request);

    /// <summary>
    /// Get account lock/unlock history logs
    /// </summary>
    Task<UserManagementResult<List<UserAccountLogViewModel>>> GetAccountLogsAsync(int customerId);

    /// <summary>
    /// Check if admin role can lock accounts
    /// </summary>
    bool CanLockAccount(string adminRole);

    /// <summary>
    /// Check if customer is VIP based on total spent
    /// </summary>
    bool IsVipCustomer(decimal totalSpent);

    /// <summary>
    /// Check account status for concurrency control
    /// </summary>
    Task<UserManagementResult<bool>> CheckAccountStatusAsync(int customerId, bool expectedIsActive);
}
