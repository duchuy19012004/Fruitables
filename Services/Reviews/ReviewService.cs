using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Options;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Outbox;
using Fruitables.ViewModels;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Reviews;

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWordMaskingService _wordMaskingService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ReviewService> _logger;
    private readonly IOutboxService? _outbox;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly bool _sentimentEnabled;

    private bool UseTargetSchema => _unitOfWork is Repositories.UnitOfWork concrete && concrete.Context.Database.IsSqlServer();

    private const int MAX_REVIEWS_PER_DAY = 100; // Tăng lên cho môi trường test
    private const int EDIT_TIME_LIMIT_HOURS = 24;
    private const int AUTO_HIDE_REPORT_THRESHOLD = 5;
    private const string RATING_CACHE_KEY_PREFIX = "product_rating_";
    private const int CACHE_DURATION_MINUTES = 10;

    public ReviewService(
        IUnitOfWork unitOfWork,
        IWordMaskingService wordMaskingService,
        IMemoryCache cache,
        ILogger<ReviewService> logger,
        IOutboxService? outbox = null,
        IOptions<SentimentOptions>? sentimentOptions = null,
        IJsonDocumentSerializer? serializer = null)
    {
        _unitOfWork = unitOfWork;
        _wordMaskingService = wordMaskingService;
        _cache = cache;
        _logger = logger;
        _outbox = outbox;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _sentimentEnabled = sentimentOptions?.Value.Enabled ?? true;
    }

    #region Customer Operations

    public async Task<ReviewResult> CreateReviewAsync(CreateReviewDto dto, int userId)
    {
        try
        {
            // 1. Validate user hasn't reviewed this product
            if (await _unitOfWork.ReviewRepository.HasUserReviewedProductAsync(userId, dto.ProductId))
            {
                return new ReviewResult
                {
                    Success = false,
                    ErrorCode = ReviewErrorCode.AlreadyReviewed,
                    ErrorMessage = "Bạn đã đánh giá sản phẩm này rồi"
                };
            }

            // 2. Check rate limit (max 5 reviews per day)
            var today = DateTime.UtcNow.Date;
            var reviewCountToday = await _unitOfWork.ReviewRepository.CountUserReviewsInPeriodAsync(userId, today);
            if (reviewCountToday >= MAX_REVIEWS_PER_DAY)
            {
                return new ReviewResult
                {
                    Success = false,
                    ErrorCode = ReviewErrorCode.RateLimitExceeded,
                    ErrorMessage = $"Bạn chỉ có thể đánh giá tối đa {MAX_REVIEWS_PER_DAY} sản phẩm mỗi ngày"
                };
            }

            // 3. Check verified purchase (optional)
            var isVerifiedPurchase = await CheckVerifiedPurchaseAsync(userId, dto.ProductId);

            // 4. Filter bad words in comment
            var filteredComment = dto.Comment;
            if (!string.IsNullOrWhiteSpace(filteredComment))
            {
                filteredComment = _wordMaskingService.MaskContent(filteredComment);
            }

            // 5. Create review
            var review = new Review
            {
                ProductId = dto.ProductId,
                UserId = userId,
                Rating = dto.Rating,
                Comment = filteredComment,
                Status = ReviewStatus.Approved, // Auto-approve for now
                IsVerifiedPurchase = isVerifiedPurchase,
                CreatedAt = DateTime.UtcNow
            };

            // Review + outbox message phải atomic: enqueue thất bại → rollback, không để lại
            // review không bao giờ được phân tích tự động. (Dispose tự rollback nếu chưa commit.)
            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            await _unitOfWork.ReviewRepository.AddAsync(review);
            await _unitOfWork.SaveChangesAsync();

            // 6. Recalculate product rating
            await RecalculateProductRatingAsync(dto.ProductId);

            // 7. Load user info for response
            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            // 8. Enqueue phân tích cảm xúc (realtime qua outbox; handler sẽ gọi LLM)
            await EnqueueSentimentAnalysisAsync(review.Id);

            await transaction.CommitAsync();

            _logger.LogInformation("User {UserId} created review {ReviewId} for product {ProductId}",
                userId, review.Id, dto.ProductId);

            return new ReviewResult
            {
                Success = true,
                Data = MapToViewModel(review, user!, userId)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review for user {UserId} and product {ProductId}", 
                userId, dto.ProductId);
            throw;
        }
    }

    public async Task<ReviewResult> UpdateReviewAsync(int reviewId, UpdateReviewDto dto, int userId)
    {
        try
        {
            var review = await _unitOfWork.ReviewRepository.GetReviewWithDetailsAsync(reviewId);
            
            if (review == null)
            {
                return new ReviewResult
                {
                    Success = false,
                    ErrorCode = ReviewErrorCode.NotFound,
                    ErrorMessage = "Không tìm thấy đánh giá"
                };
            }

            // Check ownership
            if (review.UserId != userId)
            {
                return new ReviewResult
                {
                    Success = false,
                    ErrorCode = ReviewErrorCode.Unauthorized,
                    ErrorMessage = "Bạn không có quyền chỉnh sửa đánh giá này"
                };
            }

            // Check time limit (24 hours)
            var hoursSinceCreation = (DateTime.UtcNow - review.CreatedAt).TotalHours;
            if (hoursSinceCreation > EDIT_TIME_LIMIT_HOURS)
            {
                return new ReviewResult
                {
                    Success = false,
                    ErrorCode = ReviewErrorCode.EditTimeExpired,
                    ErrorMessage = $"Chỉ có thể chỉnh sửa trong vòng {EDIT_TIME_LIMIT_HOURS} giờ sau khi tạo"
                };
            }

            // Check if deleted
            if (review.IsDeleted)
            {
                return new ReviewResult
                {
                    Success = false,
                    ErrorCode = ReviewErrorCode.NotFound,
                    ErrorMessage = "Đánh giá đã bị xóa"
                };
            }

            // Update fields
            var hasChanges = false;
            
            if (dto.Rating.HasValue && dto.Rating.Value != review.Rating)
            {
                review.Rating = dto.Rating.Value;
                hasChanges = true;
            }

            if (dto.Comment != null)
            {
                var filteredComment = _wordMaskingService.MaskContent(dto.Comment);
                if (filteredComment != review.Comment)
                {
                    review.Comment = filteredComment;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                review.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.ReviewRepository.Update(review);
                await _unitOfWork.SaveChangesAsync();

                // Recalculate if rating changed
                if (dto.Rating.HasValue)
                {
                    await RecalculateProductRatingAsync(review.ProductId);
                }

                await InvalidateSentimentAfterReviewChangeAsync(review.Id);

                // Review nội dung đổi → phân tích lại cảm xúc
                await EnqueueSentimentAnalysisAsync(review.Id, $"edit-{review.UpdatedAt?.Ticks ?? DateTime.UtcNow.Ticks}");

                _logger.LogInformation("User {UserId} updated review {ReviewId}", userId, reviewId);
            }

            return new ReviewResult
            {
                Success = true,
                Data = MapToViewModel(review, review.User, userId)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review {ReviewId} by user {UserId}", reviewId, userId);
            throw;
        }
    }

    public async Task<bool> DeleteReviewAsync(int reviewId, int userId)
    {
        try
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId);
            
            if (review == null || review.IsDeleted)
                return false;

            // Check ownership
            if (review.UserId != userId)
                return false;

            // Soft delete
            review.IsDeleted = true;
            review.DeletedAt = DateTime.UtcNow;
            
            _unitOfWork.ReviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync();

            // Recalculate product rating
            await RecalculateProductRatingAsync(review.ProductId);

            _logger.LogInformation("User {UserId} deleted review {ReviewId}", userId, reviewId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId} by user {UserId}", reviewId, userId);
            throw;
        }
    }

    public async Task<PagedResult<ReviewViewModel>> GetProductReviewsAsync(ReviewFilterDto filter, int? currentUserId = null)
    {
        try
        {
            var result = await _unitOfWork.ReviewRepository.GetProductReviewsAsync(filter);
            
            var viewModels = result.Items.Select(r => MapToViewModel(r, r.User, currentUserId)).ToList();

            return new PagedResult<ReviewViewModel>
            {
                Items = viewModels,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reviews for product {ProductId}", filter.ProductId);
            throw;
        }
    }

    public async Task<ReviewStatistics> GetProductReviewStatisticsAsync(int productId)
    {
        try
        {
            // Try get from cache
            var cacheKey = $"{RATING_CACHE_KEY_PREFIX}{productId}";
            if (_cache.TryGetValue(cacheKey, out ReviewStatistics? cachedStats) && cachedStats != null)
            {
                return cachedStats;
            }

            // Get from database
            var stats = await _unitOfWork.ReviewRepository.GetProductReviewStatisticsAsync(productId);

            // Cache for 10 minutes
            _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review statistics for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<bool> CanUserReviewProductAsync(int userId, int productId)
    {
        try
        {
            // Check if already reviewed
            var hasReviewed = await _unitOfWork.ReviewRepository.HasUserReviewedProductAsync(userId, productId);
            if (hasReviewed)
                return false;

            // Check if user has purchased the product (verified purchase)
            var hasPurchased = await CheckVerifiedPurchaseAsync(userId, productId);
            if (!hasPurchased)
                return false;

            // Check rate limit
            var today = DateTime.UtcNow.Date;
            var reviewCountToday = await _unitOfWork.ReviewRepository.CountUserReviewsInPeriodAsync(userId, today);
            if (reviewCountToday >= MAX_REVIEWS_PER_DAY)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} can review product {ProductId}", userId, productId);
            throw;
        }
    }

    public async Task<bool> ReportReviewAsync(int reviewId, ReportReviewDto dto, int userId)
    {
        try
        {
            // Check if review exists
            var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId);
            if (review == null || review.IsDeleted)
                return false;

            var metadata = ReviewAggregateJson.Read(review, _serializer);
            if (metadata.Reports.Any(item => item.ReportedByUserId == userId) ||
                (!UseTargetSchema && await _unitOfWork.ReviewReports.HasUserReportedReviewAsync(userId, reviewId)))
                return false;

            // Create report
            var report = new ReviewReport
            {
                Id = metadata.Reports.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                ReviewId = reviewId,
                ReportedByUserId = userId,
                Reason = dto.Reason,
                Description = dto.Description,
                Status = ReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            if (!UseTargetSchema)
                await _unitOfWork.ReviewReports.AddAsync(report);

            var nextCount = metadata.ReportCount + 1;
            var isHidden = metadata.IsHidden;
            string? hiddenReason = metadata.HiddenReason;
            DateTime? hiddenAt = metadata.HiddenAt;
            if (nextCount >= AUTO_HIDE_REPORT_THRESHOLD && !isHidden)
            {
                isHidden = true;
                hiddenReason = $"Tự động ẩn do có {nextCount} báo cáo";
                hiddenAt = DateTime.UtcNow;
                _logger.LogWarning("Review {ReviewId} auto-hidden due to {ReportCount} reports",
                    reviewId, nextCount);
            }

            ReviewAggregateJson.Write(review, metadata.With(
                reportCount: nextCount,
                isHidden: isHidden,
                hiddenReason: hiddenReason,
                hiddenReasonSet: true,
                hiddenAt: hiddenAt,
                hiddenAtSet: true,
                updatedAt: DateTime.UtcNow,
                updatedAtSet: true,
                reports:
                [
                    ..metadata.Reports,
                    new ReviewReportEntry
                    {
                        Id = report.Id,
                        ReportedByUserId = userId,
                        Reason = dto.Reason,
                        Description = dto.Description,
                        Status = ReportStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    }
                ]), _serializer);
            _unitOfWork.ReviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User {UserId} reported review {ReviewId}", userId, reviewId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting review {ReviewId} by user {UserId}", reviewId, userId);
            throw;
        }
    }

    public async Task<bool> MarkReviewHelpfulAsync(int reviewId, int userId)
    {
        try
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId);
            if (review == null || review.IsDeleted || review.IsHidden)
                return false;

            var metadata = ReviewAggregateJson.Read(review, _serializer);
            var alreadyVoted = metadata.HelpfulUserIds.Contains(userId) ||
                (!UseTargetSchema && _unitOfWork.ReviewHelpfuls.Query().Any(h => h.ReviewId == reviewId && h.UserId == userId));
            if (alreadyVoted)
                return false;

            // Add helpful vote record
            var helpful = new ReviewHelpful
            {
                ReviewId = reviewId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            if (!UseTargetSchema)
                await _unitOfWork.ReviewHelpfuls.AddAsync(helpful);

            ReviewAggregateJson.Write(review, metadata.With(
                helpfulCount: metadata.HelpfulCount + 1,
                helpfulUserIds: [..metadata.HelpfulUserIds, userId],
                updatedAt: DateTime.UtcNow,
                updatedAtSet: true), _serializer);
            _unitOfWork.ReviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking review {ReviewId} as helpful by user {UserId}", reviewId, userId);
            throw;
        }
    }


    #endregion

    #region Helper Methods

    private ReviewViewModel MapToViewModel(Review review, User user, int? currentUserId)
    {
        return new ReviewViewModel
        {
            Id = review.Id,
            ProductId = review.ProductId,
            UserId = review.UserId,
            UserName = user.Name,
            UserAvatar = user.Avatar,
            Rating = review.Rating,
            Comment = review.Comment,
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            HelpfulCount = review.HelpfulCount,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            IsOwner = currentUserId.HasValue && currentUserId.Value == review.UserId,
            CanEdit = currentUserId.HasValue && 
                     currentUserId.Value == review.UserId && 
                     (DateTime.UtcNow - review.CreatedAt).TotalHours <= EDIT_TIME_LIMIT_HOURS,
            HasReported = false // TODO: Check if current user has reported
        };
    }

    #endregion

    #region Admin Operations

    public async Task<PagedResult<ReviewAdminViewModel>> GetAllReviewsAsync(ReviewAdminFilterDto filter)
    {
        try
        {
            var result = await _unitOfWork.ReviewRepository.GetAllReviewsForAdminAsync(filter);
            
            var viewModels = result.Items.Select(r => new ReviewAdminViewModel
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductName = r.Product.Name,
                UserId = r.UserId,
                UserName = r.User.Name,
                UserEmail = r.User.Email,
                Rating = r.Rating,
                Comment = r.Comment,
                Status = r.Status,
                IsHidden = r.IsHidden,
                HiddenReason = r.HiddenReason,
                HiddenByAdminName = r.HiddenByAdmin?.Name,
                HiddenAt = r.HiddenAt,
                IsDeleted = r.IsDeleted,
                DeletedByAdminName = r.DeletedByAdmin?.Name,
                DeletedAt = r.DeletedAt,
                IsVerifiedPurchase = r.IsVerifiedPurchase,
                HelpfulCount = r.HelpfulCount,
                ReportCount = r.ReportCount,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return new PagedResult<ReviewAdminViewModel>
            {
                Items = viewModels,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all reviews for admin");
            throw;
        }
    }

    public async Task<bool> HideReviewAsync(int reviewId, string reason, int adminId)
    {
        try
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId);
            if (review == null || review.IsDeleted)
                return false;

            review.IsHidden = true;
            review.HiddenReason = reason;
            review.HiddenByAdminId = adminId;
            review.HiddenAt = DateTime.UtcNow;

            _unitOfWork.ReviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync();

            // Recalculate product rating (hidden reviews don't count)
            await RecalculateProductRatingAsync(review.ProductId);

            _logger.LogInformation("Admin {AdminId} hid review {ReviewId}", adminId, reviewId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding review {ReviewId} by admin {AdminId}", reviewId, adminId);
            throw;
        }
    }

    public async Task<bool> ShowReviewAsync(int reviewId, int adminId)
    {
        try
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId);
            if (review == null || review.IsDeleted)
                return false;

            review.IsHidden = false;
            review.HiddenReason = null;
            review.HiddenByAdminId = null;
            review.HiddenAt = null;

            _unitOfWork.ReviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync();

            // Recalculate product rating
            await RecalculateProductRatingAsync(review.ProductId);

            _logger.LogInformation("Admin {AdminId} showed review {ReviewId}", adminId, reviewId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing review {ReviewId} by admin {AdminId}", reviewId, adminId);
            throw;
        }
    }

    public async Task<bool> DeleteReviewByAdminAsync(int reviewId, string reason, int adminId)
    {
        try
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId);
            if (review == null || review.IsDeleted)
                return false;

            review.IsDeleted = true;
            review.DeletedByAdminId = adminId;
            review.DeletedAt = DateTime.UtcNow;
            // Store reason in HiddenReason field (reuse)
            review.HiddenReason = reason;

            _unitOfWork.ReviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync();

            // Recalculate product rating
            await RecalculateProductRatingAsync(review.ProductId);

            _logger.LogInformation("Admin {AdminId} deleted review {ReviewId}", adminId, reviewId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId} by admin {AdminId}", reviewId, adminId);
            throw;
        }
    }

    public async Task<PagedResult<ReviewReportViewModel>> GetReviewReportsAsync(ReportFilterDto filter)
    {
        if (UseTargetSchema)
            return await GetReviewReportsTargetAsync(filter);

        try
        {
            var result = await _unitOfWork.ReviewReports.GetAllReportsAsync(filter);
            
            var viewModels = result.Items.Select(rr => new ReviewReportViewModel
            {
                Id = rr.Id,
                ReviewId = rr.ReviewId,
                ReviewComment = rr.Review.Comment ?? "",
                ReviewRating = rr.Review.Rating,
                ReviewUserName = rr.Review.User.Name,
                ReviewProductName = rr.Review.Product.Name,
                ProductId = rr.Review.ProductId,
                ProductName = rr.Review.Product.Name,
                ReportedByUserId = rr.ReportedByUserId,
                ReporterName = rr.ReportedByUser.Name,
                Reason = rr.Reason,
                Description = rr.Description,
                Status = rr.Status,
                HandledByAdminName = rr.HandledByAdmin?.Name,
                HandledAt = rr.HandledAt,
                CreatedAt = rr.CreatedAt
            }).ToList();

            return new PagedResult<ReviewReportViewModel>
            {
                Items = viewModels,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review reports");
            throw;
        }
    }

    private async Task<PagedResult<ReviewReportViewModel>> GetReviewReportsTargetAsync(ReportFilterDto filter)
    {
        var reviews = await _unitOfWork.Reviews.Query()
            .AsNoTracking()
            .Include(review => review.Product)
            .Include(review => review.User)
            .Where(review => !review.IsDeleted)
            .ToListAsync();
        var reportRows = reviews.SelectMany(review => ReviewAggregateJson.Read(review, _serializer).Reports
            .Select(report => new { Review = review, Report = report }))
            .AsEnumerable();
        if (filter.Status.HasValue)
            reportRows = reportRows.Where(row => row.Report.Status == filter.Status.Value);
        if (filter.Reason.HasValue)
            reportRows = reportRows.Where(row => row.Report.Reason == filter.Reason.Value);
        if (filter.ProductId.HasValue)
            reportRows = reportRows.Where(row => row.Review.ProductId == filter.ProductId.Value);
        if (filter.ReviewId.HasValue)
            reportRows = reportRows.Where(row => row.Review.Id == filter.ReviewId.Value);
        if (filter.FromDate.HasValue)
            reportRows = reportRows.Where(row => row.Report.CreatedAt >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            reportRows = reportRows.Where(row => row.Report.CreatedAt <= filter.ToDate.Value);
        reportRows = filter.SortBy == ReportSortBy.Oldest
            ? reportRows.OrderBy(row => row.Report.CreatedAt)
            : reportRows.OrderByDescending(row => row.Report.CreatedAt);

        var materialized = reportRows.ToList();
        var userIds = materialized.Select(row => row.Report.ReportedByUserId)
            .Concat(materialized.Select(row => row.Report.HandledByAdminId ?? 0))
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        var users = await _unitOfWork.Users.Query().AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var items = materialized.Skip((page - 1) * pageSize).Take(pageSize).Select(row => new ReviewReportViewModel
        {
            Id = row.Report.Id,
            ReviewId = row.Review.Id,
            ReviewComment = row.Review.Comment ?? string.Empty,
            ReviewRating = row.Review.Rating,
            ReviewUserName = row.Review.User?.Name ?? string.Empty,
            ReviewProductName = row.Review.Product?.Name ?? string.Empty,
            ProductId = row.Review.ProductId,
            ProductName = row.Review.Product?.Name ?? string.Empty,
            ReportedByUserId = row.Report.ReportedByUserId,
            ReporterName = users.GetValueOrDefault(row.Report.ReportedByUserId)?.Name ?? string.Empty,
            Reason = row.Report.Reason,
            Description = row.Report.Description,
            Status = row.Report.Status,
            HandledByAdminName = row.Report.HandledByAdminId.HasValue
                ? users.GetValueOrDefault(row.Report.HandledByAdminId.Value)?.Name
                : null,
            HandledAt = row.Report.HandledAt,
            CreatedAt = row.Report.CreatedAt
        }).ToList();
        return new PagedResult<ReviewReportViewModel>
        {
            Items = items,
            TotalCount = materialized.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> HandleReportAsync(int reportId, ReportAction action, int adminId)
    {
        if (UseTargetSchema)
            return await HandleReportTargetAsync(reportId, action, adminId);

        try
        {
            var report = await _unitOfWork.ReviewReports
                .Query()
                .Include(rr => rr.Review)
                .FirstOrDefaultAsync(rr => rr.Id == reportId);
                
            if (report == null)
                return false;

            report.Status = action == ReportAction.Resolve ? ReportStatus.Resolved : ReportStatus.Dismissed;
            report.HandledByAdminId = adminId;
            report.HandledAt = DateTime.UtcNow;

            _unitOfWork.ReviewReports.Update(report);

            // If resolved, hide the review
            if (action == ReportAction.Resolve && !report.Review.IsHidden)
            {
                await HideReviewAsync(report.ReviewId, $"Ẩn do xử lý báo cáo #{reportId}", adminId);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} handled report {ReportId} with action {Action}", 
                adminId, reportId, action);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling report {ReportId} by admin {AdminId}", reportId, adminId);
            throw;
        }
    }

    private async Task<bool> HandleReportTargetAsync(int reportId, ReportAction action, int adminId)
    {
        var reviews = await _unitOfWork.Reviews.Query()
            .Where(review => !review.IsDeleted)
            .ToListAsync();
        foreach (var review in reviews)
        {
            var metadata = ReviewAggregateJson.Read(review, _serializer);
            var report = metadata.Reports.FirstOrDefault(item => item.Id == reportId);
            if (report is null)
                continue;
            var status = action == ReportAction.Resolve ? ReportStatus.Resolved : ReportStatus.Dismissed;
            var reports = metadata.Reports.Select(item => item.Id == reportId
                ? new ReviewReportEntry
                {
                    Id = item.Id,
                    ReportedByUserId = item.ReportedByUserId,
                    Reason = item.Reason,
                    Description = item.Description,
                    Status = status,
                    CreatedAt = item.CreatedAt,
                    HandledByAdminId = adminId,
                    HandledAt = DateTime.UtcNow
                }
                : item).ToList();
            var hidden = action == ReportAction.Resolve && !metadata.IsHidden;
            ReviewAggregateJson.Write(review, metadata.With(
                reports: reports,
                isHidden: hidden ? true : metadata.IsHidden,
                hiddenReason: hidden ? $"Ẩn do xử lý báo cáo #{reportId}" : metadata.HiddenReason,
                hiddenReasonSet: hidden,
                hiddenByAdminId: hidden ? adminId : metadata.HiddenByAdminId,
                hiddenByAdminIdSet: hidden,
                hiddenAt: hidden ? DateTime.UtcNow : metadata.HiddenAt,
                hiddenAtSet: hidden,
                updatedAt: DateTime.UtcNow,
                updatedAtSet: true), _serializer);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<ReviewAdminStatistics> GetAdminStatisticsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddMonths(-1);

            var query = _unitOfWork.Reviews.Query();
            var counts = await query
                .GroupBy(r => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Pending = g.Sum(r => r.Status == ReviewStatus.Pending && !r.IsDeleted ? 1 : 0),
                    Approved = g.Sum(r => r.Status == ReviewStatus.Approved && !r.IsDeleted ? 1 : 0),
                    Rejected = g.Sum(r => r.Status == ReviewStatus.Rejected && !r.IsDeleted ? 1 : 0),
                    Hidden = g.Sum(r => r.IsHidden && !r.IsDeleted ? 1 : 0),
                    Deleted = g.Sum(r => r.IsDeleted ? 1 : 0),
                    Reported = g.Sum(r => r.ReportCount > 0 && !r.IsDeleted ? 1 : 0),
                    Valid = g.Sum(r => !r.IsDeleted && !r.IsHidden ? 1 : 0),
                    AverageRating = g.Where(r => !r.IsDeleted && !r.IsHidden).Average(r => (decimal?)r.Rating) ?? 0,
                    Today = g.Sum(r => r.CreatedAt >= today ? 1 : 0),
                    Week = g.Sum(r => r.CreatedAt >= weekAgo ? 1 : 0),
                    Month = g.Sum(r => r.CreatedAt >= monthAgo ? 1 : 0)
                })
                .FirstOrDefaultAsync();

            var targetReportCounts = UseTargetSchema
                ? (await _unitOfWork.Reviews.Query().AsNoTracking().ToListAsync())
                    .SelectMany(review => ReviewAggregateJson.Read(review, _serializer).Reports)
                    .ToList()
                : [];
            var stats = new ReviewAdminStatistics
            {
                TotalReviews = counts?.Total ?? 0,
                PendingReviews = counts?.Pending ?? 0,
                ApprovedReviews = counts?.Approved ?? 0,
                RejectedReviews = counts?.Rejected ?? 0,
                HiddenReviews = counts?.Hidden ?? 0,
                DeletedReviews = counts?.Deleted ?? 0,
                ReportedReviews = counts?.Reported ?? 0,
                ValidReviews = counts?.Valid ?? 0,
                TotalReports = UseTargetSchema ? targetReportCounts.Count : await _unitOfWork.ReviewReports.CountAsync(),
                PendingReports = UseTargetSchema
                    ? targetReportCounts.Count(report => report.Status == ReportStatus.Pending)
                    : await _unitOfWork.ReviewReports.CountPendingReportsAsync(),
                AverageRating = counts?.AverageRating ?? 0,
                ReviewsToday = counts?.Today ?? 0,
                ReviewsThisWeek = counts?.Week ?? 0,
                ReviewsThisMonth = counts?.Month ?? 0
            };

            // Top reviewed products
            var topReviewed = await _unitOfWork.Products
                .Query()
                .OrderByDescending(p => p.ReviewCount)
                .Take(10)
                .Select(p => new TopReviewedProduct
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    ReviewCount = p.ReviewCount,
                    AverageRating = p.AverageRating
                })
                .ToListAsync();
            stats.MostReviewedProducts = topReviewed;

            // Top rated products
            var topRated = await _unitOfWork.Products
                .Query()
                .Where(p => p.ReviewCount > 0)
                .OrderByDescending(p => p.AverageRating)
                .ThenByDescending(p => p.ReviewCount)
                .Take(10)
                .Select(p => new TopRatedProduct
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    AverageRating = p.AverageRating,
                    ReviewCount = p.ReviewCount
                })
                .ToListAsync();
            stats.TopRatedProducts = topRated;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin statistics");
            throw;
        }
    }

    #endregion

    #region Internal Operations

    public async Task RecalculateProductRatingAsync(int productId)
    {
        try
        {
            var reviews = await _unitOfWork.Reviews
                .Query()
                .Where(r => r.ProductId == productId 
                    && r.Status == ReviewStatus.Approved 
                    && !r.IsHidden 
                    && !r.IsDeleted)
                .ToListAsync();

            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return;

            if (reviews.Any())
            {
                product.AverageRating = Math.Round((decimal)reviews.Average(r => r.Rating), 2);
                product.ReviewCount = reviews.Count;
            }
            else
            {
                product.AverageRating = 0;
                product.ReviewCount = 0;
            }

            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();

            // Clear cache
            var cacheKey = $"{RATING_CACHE_KEY_PREFIX}{productId}";
            _cache.Remove(cacheKey);

            _logger.LogInformation("Recalculated rating for product {ProductId}: {AverageRating} ({ReviewCount} reviews)", 
                productId, product.AverageRating, product.ReviewCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating rating for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<bool> CheckVerifiedPurchaseAsync(int userId, int productId)
    {
        try
        {
            // Check if user has an order containing this product
            // Allow review for all orders except Cancelled
            var hasOrdered = await _unitOfWork.Orders
                .Query()
                .Include(o => o.Items)
                .Where(o => o.UserId == userId &&
                       o.Status != OrderStatus.Cancelled)
                .SelectMany(o => o.Items)
                .AnyAsync(oi => oi.ProductId == productId);

            return hasOrdered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking verified purchase for user {UserId} and product {ProductId}", 
                userId, productId);
            return false;
        }
    }

    /// <summary>
    /// Kiểm tra chi tiết lý do user không thể review: đã review, chưa mua, hay được phép.
    /// </summary>
    public async Task<ReviewPermission> GetReviewPermissionAsync(int userId, int productId)
    {
        try
        {
            // Đã đánh giá rồi
            var hasReviewed = await _unitOfWork.ReviewRepository.HasUserReviewedProductAsync(userId, productId);
            if (hasReviewed)
                return ReviewPermission.AlreadyReviewed;

            // Chưa mua hàng
            var hasPurchased = await CheckVerifiedPurchaseAsync(userId, productId);
            if (!hasPurchased)
                return ReviewPermission.NotPurchased;

            // Vượt rate limit
            var today = DateTime.UtcNow.Date;
            var reviewCountToday = await _unitOfWork.ReviewRepository.CountUserReviewsInPeriodAsync(userId, today);
            if (reviewCountToday >= MAX_REVIEWS_PER_DAY)
                return ReviewPermission.RateLimitExceeded;

            return ReviewPermission.Allowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review permission for user {UserId} and product {ProductId}", userId, productId);
            return ReviewPermission.NotPurchased;
        }
    }
    /// <summary>
    /// Debug: Lấy tất cả đơn hàng của user có chứa sản phẩm
    /// </summary>
    public async Task<object> GetUserOrdersWithProductAsync(int userId, int productId)
    {
        try
        {
            var allOrders = await _unitOfWork.Orders
                .Query()
                .Include(o => o.Items)
                .Where(o => o.UserId == userId)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    Status = o.Status.ToString(),
                    o.CreatedAt,
                    Items = o.Items.Select(i => new
                    {
                        i.ProductId,
                        i.ProductName,
                        i.Quantity
                    }).ToList(),
                    HasProduct = o.Items.Any(i => i.ProductId == productId)
                })
                .ToListAsync();

            var validOrders = allOrders.Where(o => o.HasProduct).ToList();

            return new
            {
                totalOrders = allOrders.Count,
                ordersWithProduct = validOrders.Count,
                allOrders,
                validOrders
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user orders for debug");
            throw;
        }
    }

    #endregion

    // Enqueue phân tích cảm xúc cho review. Mỗi phiên bản review cần key mới,
    // nếu không idempotency key cũ sẽ chặn phân tích lại sau khi khách chỉnh sửa.
    // Không nuốt lỗi: lỗi enqueue phải nổi lên để transaction (ở luồng tạo) rollback,
    // tránh để review tồn tại mà không bao giờ được phân tích.
    private async Task EnqueueSentimentAnalysisAsync(int reviewId, string? revision = null)
    {
        if (!_sentimentEnabled || _outbox is null) return;
        await _outbox.EnqueueAsync(
            OutboxMessageTypes.ReviewSentimentAnalyze,
            new { ReviewId = reviewId },
            revision is null ? $"sentiment-{reviewId}" : $"sentiment-{reviewId}-{revision}");
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task InvalidateSentimentAfterReviewChangeAsync(int reviewId)
    {
        if (UseTargetSchema)
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId);
            if (review is null)
                return;
            var document = ReviewAggregateJson.Read(review, _serializer);
            var old = document.Sentiment;
            if (old is null)
                return;
            var failed = new ReviewSentimentPayload
            {
                Sentiment = SentimentLabel.Failed,
                RatingSentiment = old.RatingSentiment,
                CommentSentiment = null,
                HasRatingCommentConflict = false,
                NeedsManualReview = true,
                HasSafetyRisk = false,
                Severity = null,
                Confidence = null,
                Reason = "Review đã thay đổi, đang chờ phân tích lại",
                Source = SentimentSource.AiModel,
                AnalyzedAtUtc = old.AnalyzedAtUtc,
                AnalysisVersion = null,
                AlertStatus = SentimentAlertStatus.None,
                Aspects = []
            };
            ReviewAggregateJson.Write(review, document.With(sentiment: failed, sentimentSet: true), _serializer);
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        var sentiment = await _unitOfWork.ReviewSentiments
            .FirstOrDefaultAsync(s => s.ReviewId == reviewId);
        if (sentiment is null) return;

        sentiment.Sentiment = SentimentLabel.Failed;
        sentiment.CommentSentiment = null;
        sentiment.HasRatingCommentConflict = false;
        sentiment.HasSafetyRisk = false;
        sentiment.NeedsManualReview = true;
        sentiment.Severity = null;
        sentiment.Confidence = null;
        sentiment.Reason = "Review đã thay đổi, đang chờ phân tích lại";
        sentiment.Source = SentimentSource.AiModel;
        sentiment.AnalysisVersion = null;
        sentiment.AlertStatus = SentimentAlertStatus.None;
        sentiment.AdminOverrideById = null;
        sentiment.AdminOverrideAtUtc = null;
        sentiment.AdminReviewNote = null;
        _unitOfWork.ReviewSentiments.Update(sentiment);
        await _unitOfWork.SaveChangesAsync();
    }
}
