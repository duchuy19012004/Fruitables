using Fruitables.Data;
using Fruitables.Models;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Fruitables.Services.Communications;

namespace Fruitables.Services.Identity.Users;

/// <summary>
/// Service for managing customers in the admin panel
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserManagementService> _logger;
    private const decimal VIP_THRESHOLD = 50000000; // 50 million VND

    public UserManagementService(
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<UserManagementService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserListResult> GetCustomersAsync(UserFilterRequest filter)
    {
        var query = _context.Users.AsQueryable();

        if (filter.RoleType == "Admin")
        {
            query = query.Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
        }
        else
        {
            // Default to Customer
            query = query.Where(u => u.Role == UserRole.Customer);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(u => 
                u.Name.ToLower().Contains(searchTerm) || 
                u.Email.ToLower().Contains(searchTerm));
        }

        // Apply status filter
        if (filter.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == filter.IsActive.Value);
        }

        var totalCount = await query.CountAsync();

        // Apply sorting
        query = filter.SortBy?.ToLower() switch
        {
            "name" => filter.SortDescending 
                ? query.OrderByDescending(u => u.Name) 
                : query.OrderBy(u => u.Name),
            "totalorders" => filter.SortDescending 
                ? query.OrderByDescending(u => u.Orders.Count) 
                : query.OrderBy(u => u.Orders.Count),
            "totalspent" => filter.SortDescending 
                ? query.OrderByDescending(u => u.Orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total)) 
                : query.OrderBy(u => u.Orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total)),
            _ => filter.SortDescending 
                ? query.OrderByDescending(u => u.CreatedAt) 
                : query.OrderBy(u => u.CreatedAt)
        };

        // Apply pagination
        var customers = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new CustomerListItemViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                Avatar = u.Avatar,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                TotalOrders = u.Orders.Count,
                TotalSpent = u.Orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total),
                IsVip = u.Orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total) > VIP_THRESHOLD
            })
            .ToListAsync();

        return new UserListResult
        {
            Customers = customers,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
            CurrentPage = filter.Page
        };
    }

    /// <inheritdoc />
    public async Task<UserManagementResult<CustomerDetailViewModel>> GetCustomerDetailAsync(int customerId)
    {
        var customer = await _context.Users
            .Include(u => u.Orders)
            .Include(u => u.Addresses)
            .Include(u => u.LockedByAdmin)
            .FirstOrDefaultAsync(u => u.Id == customerId);

        if (customer == null)
        {
            return UserManagementResult<CustomerDetailViewModel>.NotFound(
                $"Không tìm thấy khách hàng với ID: {customerId}");
        }

        var deliveredOrders = customer.Orders.Where(o => o.Status == OrderStatus.Delivered).ToList();
        var totalSpent = deliveredOrders.Sum(o => o.Total);
        var totalOrders = customer.Orders.Count;

        var detail = new CustomerDetailViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Email = customer.Email,
            Phone = customer.Phone,
            Avatar = customer.Avatar,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt,
            LastLoginAt = customer.LastLoginAt,
            IsGoogleAccount = !string.IsNullOrEmpty(customer.GoogleId),
            IsVip = totalSpent > VIP_THRESHOLD,
            Statistics = new CustomerStatisticsViewModel
            {
                TotalOrders = totalOrders,
                TotalSpent = totalSpent,
                AverageOrderValue = totalOrders > 0 ? totalSpent / totalOrders : 0,
                CompletedOrders = deliveredOrders.Count,
                CancelledOrders = customer.Orders.Count(o => o.Status == OrderStatus.Cancelled)
            },
            Addresses = customer.Addresses.Select(a => new AddressViewModel
            {
                Id = a.Id,
                FullName = a.FullName,
                Phone = a.Phone,
                ProvinceCode = a.ProvinceCode,
                ProvinceName = a.ProvinceName,
                CommuneCode = a.CommuneCode,
                CommuneName = a.CommuneName,
                GhnDistrictId = a.GhnDistrictId,
                GhnWardCode = a.GhnWardCode,
                StreetAddress = a.StreetAddress,
                IsDefault = a.IsDefault
            }).ToList()
        };

        // Add lock info if account is locked
        if (!customer.IsActive && customer.CurrentLockType.HasValue)
        {
            detail.LockInfo = new AccountLockInfoViewModel
            {
                LockType = customer.CurrentLockType.Value,
                Reason = customer.LockReason ?? string.Empty,
                ViolationType = customer.LockViolationType ?? string.Empty,
                LockedAt = customer.LockedAt ?? DateTime.UtcNow,
                ExpiresAt = customer.LockExpiresAt,
                LockedByAdminName = customer.LockedByAdmin?.Name ?? "Unknown",
                LockedByAdminId = customer.LockedByAdminId ?? 0
            };
        }

        return UserManagementResult<CustomerDetailViewModel>.Success(detail);
    }

    /// <inheritdoc />
    public async Task<UserManagementResult<CustomerPurchaseHistoryViewModel>> GetPurchaseHistoryAsync(
        int customerId,
        OrderStatus? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 10)
    {
        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == customerId);

        if (customer == null)
        {
            return UserManagementResult<CustomerPurchaseHistoryViewModel>.NotFound(
                $"Không tìm thấy khách hàng với ID: {customerId}");
        }

        var query = _context.Orders
            .Where(o => o.UserId == customerId)
            .AsQueryable();

        // Apply status filter
        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        // Apply date range filter
        if (startDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= endDate.Value);
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new UserOrderSummaryViewModel
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                Status = o.Status,
                Total = o.Total,
                ItemCount = o.Items.Count
            })
            .ToListAsync();

        return UserManagementResult<CustomerPurchaseHistoryViewModel>.Success(
            new CustomerPurchaseHistoryViewModel
            {
                CustomerId = customerId,
                CustomerName = customer.Name,
                Orders = orders,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                CurrentPage = page
            });
    }

    /// <inheritdoc />
    public async Task<UserManagementResult<LockAccountResultViewModel>> LockAccountAsync(LockAccountRequest request)
    {
        // Check authorization
        if (!CanLockAccount(request.AdminRole))
        {
            return UserManagementResult<LockAccountResultViewModel>.Unauthorized(
                "Bạn không có quyền thực hiện hành động này");
        }

        // Check if trying to lock self
        if (request.CustomerId == request.AdminId)
        {
            return UserManagementResult<LockAccountResultViewModel>.ValidationError(
                "Không thể khóa tài khoản của chính mình");
        }

        // Validate violation type
        if (!ViolationTypes.IsValid(request.ViolationType))
        {
            return UserManagementResult<LockAccountResultViewModel>.ValidationError(
                "Loại vi phạm không hợp lệ");
        }

        // Validate reason length
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 20)
        {
            return UserManagementResult<LockAccountResultViewModel>.ValidationError(
                "Lý do khóa phải có ít nhất 20 ký tự");
        }

        // Validate lock duration for temporary lock
        if (request.LockType == LockType.Temporary)
        {
            if (!request.LockDurationDays.HasValue || 
                request.LockDurationDays < 1 || 
                request.LockDurationDays > 365)
            {
                return UserManagementResult<LockAccountResultViewModel>.ValidationError(
                    "Thời gian khóa phải từ 1 đến 365 ngày");
            }
        }

        var customer = await _context.Users
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.Id == request.CustomerId);

        if (customer == null)
        {
            return UserManagementResult<LockAccountResultViewModel>.NotFound(
                $"Không tìm thấy khách hàng với ID: {request.CustomerId}");
        }

        // Check if target is Admin/SuperAdmin
        if (customer.Role == UserRole.Admin || customer.Role == UserRole.SuperAdmin)
        {
            return UserManagementResult<LockAccountResultViewModel>.ValidationError(
                "Không thể khóa tài khoản quản trị viên");
        }

        // Check if already locked
        if (!customer.IsActive)
        {
            return UserManagementResult<LockAccountResultViewModel>.ValidationError(
                "Tài khoản này đã bị khóa");
        }

        // Check for pending orders
        var pendingOrders = customer.Orders
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing)
            .ToList();

        // Calculate total spent for VIP check
        var totalSpent = customer.Orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Sum(o => o.Total);
        var isVip = IsVipCustomer(totalSpent);

        // If VIP or has pending orders and not confirmed, return for confirmation
        if ((isVip || pendingOrders.Any()) && !request.ConfirmLockWithPendingOrders)
        {
            return UserManagementResult<LockAccountResultViewModel>.Success(
                new LockAccountResultViewModel
                {
                    Success = false,
                    HasPendingOrders = pendingOrders.Any(),
                    PendingOrderCount = pendingOrders.Count,
                    PendingOrderNumbers = pendingOrders.Select(o => o.OrderNumber).ToList(),
                    RequiresConfirmation = true,
                    IsVipCustomer = isVip
                });
        }

        // Perform the lock
        customer.IsActive = false;
        customer.CurrentLockType = request.LockType;
        customer.LockReason = request.Reason;
        customer.LockViolationType = request.ViolationType;
        customer.LockedAt = DateTime.UtcNow;
        customer.LockedByAdminId = request.AdminId;

        if (request.LockType == LockType.Temporary && request.LockDurationDays.HasValue)
        {
            customer.LockExpiresAt = DateTime.UtcNow.AddDays(request.LockDurationDays.Value);
        }
        else
        {
            customer.LockExpiresAt = null;
        }

        // Create audit log
        var log = new UserAccountLog
        {
            UserId = customer.Id,
            AdminId = request.AdminId,
            Action = "Lock",
            LockType = request.LockType,
            ViolationType = request.ViolationType,
            Reason = request.Reason,
            ExpiresAt = customer.LockExpiresAt,
            CreatedAt = DateTime.UtcNow,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        };
        _context.UserAccountLogs.Add(log);

        await _context.SaveChangesAsync();

        // Send email notification to customer (Requirements 4.7)
        try
        {
            await _emailService.SendAccountLockedEmailAsync(
                customer.Email,
                customer.Name,
                request.ViolationType,
                request.Reason,
                request.LockType.ToString(),
                customer.LockExpiresAt);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the lock operation
            _logger.LogError(ex, 
                "Failed to send account locked email to customer {CustomerId}", 
                customer.Id);
        }

        return UserManagementResult<LockAccountResultViewModel>.Success(
            new LockAccountResultViewModel
            {
                Success = true,
                HasPendingOrders = pendingOrders.Any(),
                PendingOrderCount = pendingOrders.Count,
                PendingOrderNumbers = pendingOrders.Select(o => o.OrderNumber).ToList(),
                RequiresConfirmation = false,
                IsVipCustomer = isVip
            });
    }

    /// <inheritdoc />
    public async Task<UserManagementResult<bool>> UnlockAccountAsync(UnlockAccountRequest request)
    {
        // Check authorization
        if (!CanLockAccount(request.AdminRole))
        {
            return UserManagementResult<bool>.Unauthorized(
                "Bạn không có quyền thực hiện hành động này");
        }

        // Validate reason length
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 10)
        {
            return UserManagementResult<bool>.ValidationError(
                "Lý do mở khóa phải có ít nhất 10 ký tự");
        }

        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.CustomerId);

        if (customer == null)
        {
            return UserManagementResult<bool>.NotFound(
                $"Không tìm thấy khách hàng với ID: {request.CustomerId}");
        }

        // Check if account is active
        if (customer.IsActive)
        {
            return UserManagementResult<bool>.ValidationError(
                "Tài khoản này đang hoạt động");
        }

        // Perform the unlock
        customer.IsActive = true;
        customer.CurrentLockType = null;
        customer.LockReason = null;
        customer.LockViolationType = null;
        customer.LockedAt = null;
        customer.LockExpiresAt = null;
        customer.LockedByAdminId = null;

        // Create audit log
        var log = new UserAccountLog
        {
            UserId = customer.Id,
            AdminId = request.AdminId,
            Action = "Unlock",
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        };
        _context.UserAccountLogs.Add(log);

        await _context.SaveChangesAsync();

        // Send email notification to customer (Requirements 5.4)
        try
        {
            await _emailService.SendAccountUnlockedEmailAsync(
                customer.Email,
                customer.Name,
                request.Reason);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the unlock operation
            _logger.LogError(ex, 
                "Failed to send account unlocked email to customer {CustomerId}", 
                customer.Id);
        }

        return UserManagementResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<UserManagementResult<List<UserAccountLogViewModel>>> GetAccountLogsAsync(int customerId)
    {
        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == customerId);

        if (customer == null)
        {
            return UserManagementResult<List<UserAccountLogViewModel>>.NotFound(
                $"Không tìm thấy khách hàng với ID: {customerId}");
        }

        var logs = await _context.UserAccountLogs
            .Where(l => l.UserId == customerId)
            .Include(l => l.Admin)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new UserAccountLogViewModel
            {
                Id = l.Id,
                CreatedAt = l.CreatedAt,
                Action = l.Action,
                LockType = l.LockType,
                ViolationType = l.ViolationType,
                Reason = l.Reason,
                AdminName = l.Admin.Name,
                AdminId = l.AdminId,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent
            })
            .ToListAsync();

        return UserManagementResult<List<UserAccountLogViewModel>>.Success(logs);
    }

    /// <inheritdoc />
    public bool CanLockAccount(string adminRole)
    {
        return adminRole == "SuperAdmin";
    }

    /// <inheritdoc />
    public bool IsVipCustomer(decimal totalSpent)
    {
        return totalSpent > VIP_THRESHOLD;
    }

    /// <inheritdoc />
    public async Task<UserManagementResult<bool>> CheckAccountStatusAsync(int customerId, bool expectedIsActive)
    {
        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == customerId);

        if (customer == null)
        {
            return UserManagementResult<bool>.NotFound(
                $"Không tìm thấy khách hàng với ID: {customerId}");
        }

        if (customer.IsActive != expectedIsActive)
        {
            var admin = customer.LockedByAdminId.HasValue 
                ? await _context.Users.FindAsync(customer.LockedByAdminId.Value)
                : null;
            
            return UserManagementResult<bool>.ConcurrencyError(
                "Trạng thái tài khoản đã thay đổi",
                admin?.Name ?? "Unknown",
                customer.LockedAt ?? DateTime.UtcNow);
        }

        return UserManagementResult<bool>.Success(true);
    }
}
