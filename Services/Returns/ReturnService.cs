using System.ComponentModel.DataAnnotations;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Orders;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fruitables.Services.Returns;

public sealed class ReturnService : IReturnService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageUploadService _imageUploadService;
    private readonly TimeProvider _timeProvider;

    public ReturnService(
        ApplicationDbContext context,
        IImageUploadService imageUploadService,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _imageUploadService = imageUploadService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ReturnEligibilityViewModel> GetEligibilityAsync(int orderId, int userId)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(item => item.Id == orderId && item.UserId == userId);
        var existing = await _context.ReturnRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OrderId == orderId);

        var deadline = order?.DeliveredAtUtc?.AddHours(24);
        var canCreate = order != null &&
                        order.Status == OrderStatus.Delivered &&
                        deadline.HasValue &&
                        _timeProvider.GetUtcNow().UtcDateTime <= deadline.Value &&
                        existing == null;

        return new ReturnEligibilityViewModel
        {
            OrderId = orderId,
            OrderNumber = order?.OrderNumber ?? string.Empty,
            CanCreate = canCreate,
            DeliveredAtUtc = order?.DeliveredAtUtc,
            ClaimDeadlineAtUtc = deadline,
            ExistingRequestId = existing?.Id,
            Items = order?.Items.Select(item => new ReturnEligibleItemViewModel
            {
                OrderItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Unit = item.Product?.Unit ?? string.Empty,
                OrderedQuantity = item.Quantity,
                MaxClaimableQuantity = item.Quantity
            }).ToList() ?? []
        };
    }

    public async Task<ReturnOperationResult> CreateAsync(CreateReturnCommand command, int userId)
    {
        if (command.Items.Count == 0)
            return Fail("Vui lòng chọn ít nhất một sản phẩm.");

        var order = await _context.Orders
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
            .Include(item => item.Items)
                .ThenInclude(item => item.ProductVariant)
            .FirstOrDefaultAsync(item => item.Id == command.OrderId && item.UserId == userId);
        if (order == null)
            return Fail("Đơn hàng không tồn tại hoặc không thuộc tài khoản của bạn.");
        if (order.Status != OrderStatus.Delivered || !order.DeliveredAtUtc.HasValue)
            return Fail("Chỉ có thể khiếu nại đơn đã giao.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var deadline = order.DeliveredAtUtc.Value.AddHours(24);
        if (now > deadline)
            return Fail("Đã quá thời hạn 24 giờ để gửi khiếu nại.");
        if (await _context.ReturnRequests.AnyAsync(item => item.OrderId == order.Id))
            return Fail("Đơn hàng này đã có yêu cầu khiếu nại.");
        if (command.Items.Select(item => item.OrderItemId).Distinct().Count() != command.Items.Count)
            return Fail("Mỗi sản phẩm chỉ được xuất hiện một lần trong yêu cầu.");

        var orderItems = command.Items.ToDictionary(item => item.OrderItemId);
        var requestItems = new List<ReturnRequestItem>();
        foreach (var orderItem in order.Items)
        {
            if (!orderItems.TryGetValue(orderItem.Id, out var itemCommand))
                continue;

            var validationError = ValidateItem(orderItem, itemCommand);
            if (validationError != null)
                return Fail(validationError);

            requestItems.Add(new ReturnRequestItem
            {
                OrderItemId = orderItem.Id,
                OrderItem = orderItem,
                RequestedQuantity = itemCommand.RequestedQuantity,
                ApprovedQuantity = 0,
                Reason = itemCommand.Reason,
                Description = itemCommand.Description.Trim(),
                RequestedAmount = CalculatePaidLineAmount(order, orderItem, itemCommand.RequestedQuantity)
            });
        }

        if (requestItems.Count != command.Items.Count)
            return Fail("Một số sản phẩm không thuộc đơn hàng.");

        var request = new ReturnRequest
        {
            ReturnNumber = CreateReturnNumber(now),
            Order = order,
            UserId = userId,
            Status = ReturnRequestStatus.Submitted,
            SubmittedAtUtc = now,
            ClaimDeadlineAtUtc = deadline,
            CustomerNote = string.Join("\n", requestItems.Select(item => item.Description)),
            Items = requestItems,
            RequestedAmount = requestItems.Sum(item => item.RequestedAmount),
            RowVersion = NewRowVersion()
        };
        AddEvent(request, ReturnEventType.Submitted, null, ReturnRequestStatus.Submitted, userId, "Khách gửi yêu cầu khiếu nại.");

        var uploadedKeys = new List<string>();
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await BeginTransactionAsync();
            for (var index = 0; index < command.Items.Count; index++)
            {
                var commandItem = command.Items[index];
                var requestItem = requestItems.Single(item => item.OrderItemId == commandItem.OrderItemId);
                foreach (var file in commandItem.Evidence)
                {
                    var storageKey = await UploadEvidenceAsync(file, request.ReturnNumber, uploadedKeys);
                    requestItem.Evidence.Add(new ReturnEvidence
                    {
                        ReturnRequest = request,
                        ReturnRequestItem = requestItem,
                        StorageKey = storageKey,
                        OriginalFileName = Path.GetFileName(file.FileName)[..Math.Min(255, Path.GetFileName(file.FileName).Length)],
                        ContentType = file.ContentType,
                        SizeBytes = file.Length,
                        UploadedByUserId = userId,
                        UploadedAtUtc = now
                    });
                }
            }

            _context.ReturnRequests.Add(request);
            await _context.SaveChangesAsync();
            if (transaction != null)
                await transaction.CommitAsync();

            return Success(request);
        }
        catch (DbUpdateException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            await DeleteUploadedFilesAsync(uploadedKeys);
            return Fail("Không thể lưu yêu cầu. Có thể đơn hàng vừa được tạo yêu cầu khác.");
        }
        catch (Exception exception)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            await DeleteUploadedFilesAsync(uploadedKeys);
            return Fail($"Không thể lưu yêu cầu: {exception.Message}");
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<ReturnDetailViewModel?> GetCustomerDetailAsync(int returnRequestId, int userId)
    {
        var request = await LoadRequestAsync(returnRequestId, userId);
        return request == null ? null : Map(request);
    }

    public async Task<ReturnOperationResult> CancelAsync(int returnRequestId, int userId)
    {
        var request = await LoadRequestAsync(returnRequestId, userId);
        if (request == null)
            return Fail("Không tìm thấy yêu cầu.");
        if (request.Items.Any(item => item.DecisionStatus != ReturnItemDecisionStatus.Pending) ||
            request.Status is ReturnRequestStatus.AwaitingRefund or ReturnRequestStatus.Refunded or ReturnRequestStatus.Rejected or ReturnRequestStatus.Cancelled)
            return Fail("Yêu cầu không còn có thể hủy.");

        var oldStatus = request.Status;
        request.Status = ReturnRequestStatus.Cancelled;
        AddEvent(request, ReturnEventType.Cancelled, oldStatus, request.Status, userId, "Khách đã hủy yêu cầu.");
        return await SaveStatusChangeAsync(request);
    }

    public async Task<ReturnOperationResult> RequestCustomerInfoAsync(
        RequestCustomerInfoCommand command,
        int adminId)
    {
        var request = await LoadRequestAsync(command.ReturnRequestId);
        if (request == null)
            return Fail("Không tìm thấy yêu cầu.");
        if (request.SupplementCount > 0 || request.Status is ReturnRequestStatus.AwaitingCustomerInfo or ReturnRequestStatus.AwaitingRefund or ReturnRequestStatus.Refunded or ReturnRequestStatus.Rejected or ReturnRequestStatus.Cancelled)
            return Fail("Yêu cầu không còn cho phép bổ sung thông tin.");
        if (!HasMatchingRowVersion(request, command.RowVersion))
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");
        if (string.IsNullOrWhiteSpace(command.Note))
            return Fail("Vui lòng ghi rõ thông tin cần bổ sung.");

        var oldStatus = request.Status;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        request.Status = ReturnRequestStatus.AwaitingCustomerInfo;
        request.SupplementDeadlineAtUtc = now.AddHours(24);
        request.AdminNote = command.Note.Trim();
        AddEvent(request, ReturnEventType.CustomerInfoRequested, oldStatus, request.Status, adminId, request.AdminNote);
        return await SaveStatusChangeAsync(request);
    }

    public async Task<ReturnOperationResult> DecideAsync(
        DecideReturnCommand command,
        int adminId)
    {
        var request = await LoadRequestAsync(command.ReturnRequestId);
        if (request == null)
            return Fail("Không tìm thấy yêu cầu.");
        if (!HasMatchingRowVersion(request, command.RowVersion))
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");
        if (request.Status is ReturnRequestStatus.AwaitingRefund or ReturnRequestStatus.Refunded or ReturnRequestStatus.Rejected or ReturnRequestStatus.Cancelled)
            return Fail("Yêu cầu không còn chờ quyết định.");
        if (command.Items.Count != request.Items.Count ||
            command.Items.Select(item => item.OrderItemId).Distinct().Count() != command.Items.Count)
            return Fail("Cần quyết định cho từng sản phẩm trong yêu cầu.");

        var decisions = command.Items.ToDictionary(item => item.OrderItemId);
        foreach (var requestItem in request.Items)
        {
            if (!decisions.TryGetValue(requestItem.OrderItemId, out var decision))
                return Fail("Cần quyết định cho từng sản phẩm trong yêu cầu.");

            var isReduced = decision.Approved && decision.ApprovedQuantity < requestItem.RequestedQuantity;
            if ((!decision.Approved || isReduced) && string.IsNullOrWhiteSpace(decision.DecisionReason))
                return Fail("Phải ghi lý do khi từ chối hoặc duyệt ít hơn số lượng yêu cầu.");
            if (decision.Approved)
            {
                var unit = requestItem.OrderItem.Product?.Unit;
                var step = string.Equals(unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase) ? 0.1m : 1m;
                if (decision.ApprovedQuantity <= 0 ||
                    decision.ApprovedQuantity > requestItem.RequestedQuantity ||
                    !QuantityRules.IsValid(unit, decision.ApprovedQuantity, step))
                    return Fail("Số lượng được duyệt không hợp lệ.");
            }
        }

        var order = request.Order;
        foreach (var requestItem in request.Items)
        {
            var decision = decisions[requestItem.OrderItemId];
            var approved = decision.Approved;
            var approvedQuantity = approved ? decision.ApprovedQuantity : 0m;
            requestItem.DecisionStatus = approved
                ? ReturnItemDecisionStatus.Approved
                : ReturnItemDecisionStatus.Rejected;
            requestItem.ApprovedQuantity = approvedQuantity;
            requestItem.ApprovedAmount = approved
                ? CalculatePaidLineAmount(order, requestItem.OrderItem, approvedQuantity)
                : 0m;
            requestItem.DecisionReason = string.IsNullOrWhiteSpace(decision.DecisionReason)
                ? null
                : decision.DecisionReason.Trim();
            AddEvent(
                request,
                approved ? ReturnEventType.Approved : ReturnEventType.Rejected,
                request.Status,
                request.Status,
                adminId,
                requestItem.DecisionReason);
        }

        var approvedItems = request.Items.Where(item => item.DecisionStatus == ReturnItemDecisionStatus.Approved).ToList();
        request.ApprovedAmount = decimal.Round(approvedItems.Sum(item => item.ApprovedAmount), 2);
        var fullOrderFault = command.RefundShippingFee &&
            order.Items.Count > 0 &&
            order.Items.All(orderItem => request.Items.Any(requestItem =>
                requestItem.OrderItemId == orderItem.Id &&
                requestItem.DecisionStatus == ReturnItemDecisionStatus.Approved &&
                requestItem.ApprovedQuantity == orderItem.Quantity));
        request.ApprovedShippingFeeAmount = fullOrderFault ? order.ShippingFee : 0m;
        request.AdminNote = command.DecisionNote.Trim();

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await BeginTransactionAsync();
            if (approvedItems.Count == 0)
            {
                var oldStatus = request.Status;
                request.Status = ReturnRequestStatus.Rejected;
                AddEvent(request, ReturnEventType.Rejected, oldStatus, request.Status, adminId, request.AdminNote);
            }
            else
            {
                request.Status = ReturnRequestStatus.AwaitingRefund;
                if (request.Refund != null)
                    return Fail("Yêu cầu đã có khoản hoàn.");
                request.Refund = new Refund
                {
                    ReturnRequest = request,
                    Order = order,
                    OrderId = order.Id,
                    Amount = request.ApprovedAmount + request.ApprovedShippingFeeAmount,
                    ShippingFeeAmount = request.ApprovedShippingFeeAmount,
                    Status = RefundStatus.Pending,
                    CreatedByUserId = adminId,
                    CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
                };
                AddEvent(request, ReturnEventType.RefundCreated, ReturnRequestStatus.UnderReview, request.Status, adminId, command.DecisionNote.Trim());
            }

            TouchRowVersion(request);
            await _context.SaveChangesAsync();
            if (transaction != null)
                await transaction.CommitAsync();
            return Success(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");
        }
        catch (DbUpdateException exception)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return Fail($"Không thể lưu quyết định: {exception.InnerException?.Message ?? exception.Message}");
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<ReturnOperationResult> CompleteRefundAsync(
        CompleteRefundCommand command,
        int adminId)
    {
        var request = await LoadRequestAsync(command.ReturnRequestId);
        if (request?.Refund == null)
            return Fail("Không tìm thấy khoản hoàn.");
        if (request.Status != ReturnRequestStatus.AwaitingRefund)
            return Fail("Yêu cầu không còn chờ hoàn tiền.");
        if (!HasMatchingRowVersion(request, command.RowVersion))
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");

        if (command.Succeeded && string.IsNullOrWhiteSpace(command.TransactionReference))
            return Fail("Cần nhập mã giao dịch hoàn tiền.");
        if (!command.Succeeded && string.IsNullOrWhiteSpace(command.FailureReason))
            return Fail("Cần ghi lý do chuyển tiền thất bại.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await BeginTransactionAsync();
            if (request.Refund.Status == RefundStatus.Succeeded)
                return Fail("Khoản hoàn đã được xác nhận trước đó.");

            request.Refund.Status = command.Succeeded ? RefundStatus.Succeeded : RefundStatus.Failed;
            request.Refund.TransactionReference = command.Succeeded ? command.TransactionReference!.Trim() : null;
            request.Refund.FailureReason = command.Succeeded ? null : command.FailureReason!.Trim();
            request.Refund.ProcessedByUserId = adminId;
            request.Refund.ProcessedAtUtc = now;

            if (command.Succeeded)
            {
                var oldStatus = request.Status;
                request.Status = ReturnRequestStatus.Refunded;
                AddEvent(request, ReturnEventType.RefundCompleted, oldStatus, request.Status, adminId, request.Refund.TransactionReference);
                var successfulRefundTotal = request.Refund.Amount + (await _context.Refunds
                    .Where(refund => refund.OrderId == request.OrderId &&
                        refund.Id != request.Refund.Id &&
                        refund.Status == RefundStatus.Succeeded)
                    .ToListAsync())
                    .Sum(refund => refund.Amount);
                if (successfulRefundTotal + 0.005m >= request.Order.Total)
                    request.Order.PaymentStatus = PaymentStatus.Refunded;
            }
            else
            {
                AddEvent(request, ReturnEventType.RefundFailed, request.Status, request.Status, adminId, request.Refund.FailureReason);
            }

            TouchRowVersion(request);
            await _context.SaveChangesAsync();
            if (transaction != null)
                await transaction.CommitAsync();
            return Success(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");
        }
        catch (DbUpdateException exception)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return Fail($"Không thể xác nhận hoàn tiền: {exception.InnerException?.Message ?? exception.Message}");
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<ReturnOperationResult> AddCustomerInfoAsync(
        SupplementReturnCommand command,
        int userId)
    {
        var request = await LoadRequestAsync(command.ReturnRequestId, userId);
        if (request == null)
            return Fail("Không tìm thấy yêu cầu.");
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (request.Status == ReturnRequestStatus.AwaitingCustomerInfo &&
            request.SupplementDeadlineAtUtc.HasValue &&
            now >= request.SupplementDeadlineAtUtc.Value)
        {
            var oldStatus = request.Status;
            request.Status = ReturnRequestStatus.Rejected;
            request.AdminNote = "Khách không bổ sung thông tin trong 24 giờ.";
            AddEvent(request, ReturnEventType.Rejected, oldStatus, request.Status, userId, request.AdminNote);
            var expiryResult = await SaveStatusChangeAsync(request);
            return expiryResult.Success
                ? Fail("Đã quá hạn bổ sung thông tin; yêu cầu đã bị từ chối.")
                : expiryResult;
        }
        if (request.Status != ReturnRequestStatus.AwaitingCustomerInfo ||
            request.SupplementCount > 0 ||
            !request.SupplementDeadlineAtUtc.HasValue)
            return Fail("Yêu cầu không còn nhận bổ sung thông tin.");
        if (!HasMatchingRowVersion(request, command.RowVersion))
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");
        if (string.IsNullOrWhiteSpace(command.Description) && command.Evidence.Count == 0)
            return Fail("Vui lòng bổ sung mô tả hoặc ảnh.");

        var uploadedKeys = new List<string>();
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await BeginTransactionAsync();
            foreach (var file in command.Evidence)
            {
                var storageKey = await UploadEvidenceAsync(file, request.ReturnNumber, uploadedKeys);
                request.Evidence.Add(new ReturnEvidence
                {
                    ReturnRequest = request,
                    StorageKey = storageKey,
                    OriginalFileName = Path.GetFileName(file.FileName)[..Math.Min(255, Path.GetFileName(file.FileName).Length)],
                    ContentType = file.ContentType,
                    SizeBytes = file.Length,
                    UploadedByUserId = userId,
                    UploadedAtUtc = now
                });
            }

            var oldStatus = request.Status;
            request.Status = ReturnRequestStatus.UnderReview;
            request.SupplementCount++;
            request.SupplementDeadlineAtUtc = null;
            request.CustomerNote = command.Description.Trim();
            AddEvent(request, ReturnEventType.CustomerInfoAdded, oldStatus, request.Status, userId, request.CustomerNote);
            TouchRowVersion(request);
            await _context.SaveChangesAsync();
            if (transaction != null)
                await transaction.CommitAsync();

            return Success(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            await DeleteUploadedFilesAsync(uploadedKeys);
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");
        }
        catch (Exception exception)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            await DeleteUploadedFilesAsync(uploadedKeys);
            return Fail($"Không thể lưu thông tin bổ sung: {exception.Message}");
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<ReturnRequest?> LoadRequestAsync(int returnRequestId, int? userId = null)
    {
        var query = _context.ReturnRequests
            .Include(request => request.Order)
                .ThenInclude(order => order.Items)
                    .ThenInclude(item => item.Product)
            .Include(request => request.Order)
                .ThenInclude(order => order.Items)
                    .ThenInclude(item => item.ProductVariant)
            .Include(request => request.Items)
                .ThenInclude(item => item.OrderItem)
                    .ThenInclude(item => item.Product)
            .Include(request => request.Evidence)
            .Include(request => request.Events)
            .Include(request => request.Refund)
            .AsQueryable();
        if (userId.HasValue)
            query = query.Where(request => request.UserId == userId.Value);
        return await query.FirstOrDefaultAsync(request => request.Id == returnRequestId);
    }

    private async Task<ReturnOperationResult> SaveStatusChangeAsync(ReturnRequest request)
    {
        try
        {
            TouchRowVersion(request);
            await _context.SaveChangesAsync();
            return Success(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail("Yêu cầu đã được cập nhật. Vui lòng tải lại dữ liệu.");
        }
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if ((_context.Database.ProviderName ?? string.Empty).Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return null;
        return await _context.Database.BeginTransactionAsync();
    }

    private async Task<string> UploadEvidenceAsync(
        IFormFile file,
        string returnNumber,
        ICollection<string> uploadedKeys)
    {
        if (!_imageUploadService.IsValidImageFile(file) || !_imageUploadService.IsValidFileSize(file))
            throw new ValidationException("Ảnh xác minh không hợp lệ.");
        var key = await _imageUploadService.UploadImageAsync(file, $"returns/{returnNumber}");
        uploadedKeys.Add(key);
        return key;
    }

    private async Task DeleteUploadedFilesAsync(IEnumerable<string> storageKeys)
    {
        foreach (var key in storageKeys)
        {
            try
            {
                await _imageUploadService.DeleteImageAsync(key);
            }
            catch
            {
                // The database failure is the actionable error; cleanup is best effort.
            }
        }
    }

    private static string? ValidateItem(OrderItem orderItem, CreateReturnItemCommand command)
    {
        if (!Enum.IsDefined(command.Reason))
            return "Lý do khiếu nại không hợp lệ.";
        if (command.RequestedQuantity <= 0 || command.RequestedQuantity > orderItem.Quantity)
            return "Số lượng khiếu nại không hợp lệ hoặc vượt số lượng đã mua.";
        var step = string.Equals(orderItem.Product?.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)
            ? 0.1m
            : 1m;
        if (!QuantityRules.IsValid(orderItem.Product?.Unit, command.RequestedQuantity, step))
            return "Số lượng phải theo đúng bước của sản phẩm.";
        if (string.IsNullOrWhiteSpace(command.Description))
            return "Vui lòng nhập mô tả sự cố.";
        if (RequiresEvidence(command.Reason) && command.Evidence.Count == 0)
            return "Lý do này cần ít nhất một ảnh xác minh.";
        return null;
    }

    private static bool RequiresEvidence(ReturnReasonCode reason) =>
        reason is ReturnReasonCode.Damaged or ReturnReasonCode.Mold or ReturnReasonCode.NotFresh;

    private static decimal CalculatePaidLineAmount(Order order, OrderItem item, decimal quantity)
    {
        var productTotal = order.Items.Sum(orderItem => orderItem.Total);
        var allocatedDiscount = productTotal <= 0 ? 0 : order.Discount * item.Total / productTotal;
        var paidLine = Math.Max(0, item.Total - allocatedDiscount);
        return item.Quantity <= 0 ? 0 : decimal.Round(paidLine * quantity / item.Quantity, 2);
    }

    private static void AddEvent(
        ReturnRequest request,
        ReturnEventType eventType,
        ReturnRequestStatus? oldStatus,
        ReturnRequestStatus? newStatus,
        int? actorUserId,
        string? note)
    {
        request.Events.Add(new ReturnEvent
        {
            ReturnRequest = request,
            EventType = eventType,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ActorUserId = actorUserId,
            Note = note,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static string CreateReturnNumber(DateTime now) =>
        $"RET-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";

    private static byte[] NewRowVersion() => Guid.NewGuid().ToByteArray();

    private static void TouchRowVersion(ReturnRequest request)
    {
        request.RowVersion = NewRowVersion();
    }

    private static bool HasMatchingRowVersion(ReturnRequest request, string token)
    {
        try
        {
            return Convert.FromBase64String(token).AsSpan().SequenceEqual(request.RowVersion ?? []);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static ReturnOperationResult Success(ReturnRequest request) => new()
    {
        Success = true,
        ReturnRequestId = request.Id,
        RowVersion = Convert.ToBase64String(request.RowVersion ?? NewRowVersion()),
        ApprovedShippingFeeAmount = request.ApprovedShippingFeeAmount
    };

    private static ReturnOperationResult Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    private static ReturnDetailViewModel Map(ReturnRequest request) => new()
    {
        Id = request.Id,
        ReturnNumber = request.ReturnNumber,
        OrderId = request.OrderId,
        UserId = request.UserId,
        Status = request.Status,
        SubmittedAtUtc = request.SubmittedAtUtc,
        ClaimDeadlineAtUtc = request.ClaimDeadlineAtUtc,
        SupplementDeadlineAtUtc = request.SupplementDeadlineAtUtc,
        RequestedAmount = request.RequestedAmount,
        ApprovedAmount = request.ApprovedAmount,
        ApprovedShippingFeeAmount = request.ApprovedShippingFeeAmount,
        CustomerNote = request.CustomerNote,
        AdminNote = request.AdminNote,
        RowVersion = Convert.ToBase64String(request.RowVersion ?? []),
        Items = request.Items.OrderBy(item => item.Id).Select(item => new ReturnItemDetailViewModel
        {
            Id = item.Id,
            OrderItemId = item.OrderItemId,
            ProductName = item.OrderItem.ProductName,
            Unit = item.OrderItem.Product?.Unit ?? string.Empty,
            Reason = item.Reason,
            DecisionStatus = item.DecisionStatus,
            OrderedQuantity = item.OrderItem.Quantity,
            RequestedQuantity = item.RequestedQuantity,
            ApprovedQuantity = item.ApprovedQuantity,
            RequestedAmount = item.RequestedAmount,
            ApprovedAmount = item.ApprovedAmount,
            Description = item.Description,
            DecisionReason = item.DecisionReason,
            Evidence = request.Evidence.Where(evidence => evidence.ReturnRequestItemId == item.Id)
                .Select(MapEvidence)
                .ToList()
        }).ToList(),
        Evidence = request.Evidence.Where(evidence => evidence.ReturnRequestItemId == null).Select(MapEvidence).ToList(),
        Events = request.Events.OrderBy(item => item.CreatedAtUtc).Select(item => new ReturnEventViewModel
        {
            Id = item.Id,
            EventType = item.EventType,
            OldStatus = item.OldStatus,
            NewStatus = item.NewStatus,
            Note = item.Note,
            CreatedAtUtc = item.CreatedAtUtc
        }).ToList(),
        Refund = request.Refund == null ? null : new RefundViewModel
        {
            Id = request.Refund.Id,
            Amount = request.Refund.Amount,
            ShippingFeeAmount = request.Refund.ShippingFeeAmount,
            Status = request.Refund.Status,
            TransactionReference = request.Refund.TransactionReference,
            FailureReason = request.Refund.FailureReason
        }
    };

    private static ReturnEvidenceViewModel MapEvidence(ReturnEvidence evidence) => new()
    {
        Id = evidence.Id,
        StorageKey = evidence.StorageKey,
        OriginalFileName = evidence.OriginalFileName,
        ContentType = evidence.ContentType,
        SizeBytes = evidence.SizeBytes,
        UploadedAtUtc = evidence.UploadedAtUtc
    };
}
