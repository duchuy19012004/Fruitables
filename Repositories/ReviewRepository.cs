using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly DbSet<Review> _dbSet;

    public ReviewRepository(ApplicationDbContext context)
    {
        _dbSet = context.Reviews;
    }

    public async Task<PagedResult<Review>> GetProductReviewsAsync(ReviewFilterDto filter)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.ProductId == filter.ProductId 
                && r.Status == ReviewStatus.Approved 
                && !r.IsHidden 
                && !r.IsDeleted);

        // Filter by rating
        if (filter.RatingFilter.HasValue)
        {
            query = query.Where(r => r.Rating == filter.RatingFilter.Value);
        }

        // Sort
        query = filter.SortBy switch
        {
            ReviewSortBy.Newest => query.OrderByDescending(r => r.CreatedAt),
            ReviewSortBy.Oldest => query.OrderBy(r => r.CreatedAt),
            ReviewSortBy.HighestRating => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            ReviewSortBy.LowestRating => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            ReviewSortBy.MostHelpful => query.OrderByDescending(r => r.HelpfulCount).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<Review>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<Review?> GetReviewWithDetailsAsync(int reviewId)
    {
        return await _dbSet
            .Include(r => r.User)
            .Include(r => r.Product)
            .Include(r => r.HiddenByAdmin)
            .Include(r => r.DeletedByAdmin)
            .Include(r => r.Reports)
            .FirstOrDefaultAsync(r => r.Id == reviewId);
    }

    public async Task<Review?> GetUserReviewForProductAsync(int userId, int productId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId && !r.IsDeleted);
    }

    public async Task<ReviewStatistics> GetProductReviewStatisticsAsync(int productId)
    {
        var reviews = await _dbSet
            .AsNoTracking()
            .Where(r => r.ProductId == productId 
                && r.Status == ReviewStatus.Approved 
                && !r.IsHidden 
                && !r.IsDeleted)
            .ToListAsync();

        if (!reviews.Any())
        {
            return new ReviewStatistics
            {
                TotalReviews = 0,
                AverageRating = 0,
                RatingDistribution = new Dictionary<int, int>(),
                RatingPercentages = new Dictionary<int, decimal>()
            };
        }

        var totalReviews = reviews.Count;
        var averageRating = (decimal)reviews.Average(r => r.Rating);

        var ratingDistribution = new Dictionary<int, int>();
        var ratingPercentages = new Dictionary<int, decimal>();

        for (int i = 1; i <= 5; i++)
        {
            var count = reviews.Count(r => r.Rating == i);
            ratingDistribution[i] = count;
            ratingPercentages[i] = totalReviews > 0 ? Math.Round((decimal)count / totalReviews * 100, 2) : 0;
        }

        return new ReviewStatistics
        {
            TotalReviews = totalReviews,
            AverageRating = Math.Round(averageRating, 2),
            RatingDistribution = ratingDistribution,
            RatingPercentages = ratingPercentages
        };
    }

    public async Task<PagedResult<Review>> GetAllReviewsForAdminAsync(ReviewAdminFilterDto filter)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Product)
            .Include(r => r.HiddenByAdmin)
            .Include(r => r.DeletedByAdmin)
            .AsQueryable();

        // Filters
        if (filter.ProductId.HasValue)
            query = query.Where(r => r.ProductId == filter.ProductId.Value);

        if (filter.UserId.HasValue)
            query = query.Where(r => r.UserId == filter.UserId.Value);

        if (!string.IsNullOrEmpty(filter.Status))
        {
            query = filter.Status.ToLower() switch
            {
                "visible" => query.Where(r => !r.IsHidden && !r.IsDeleted),
                "hidden" => query.Where(r => r.IsHidden && !r.IsDeleted),
                "deleted" => query.Where(r => r.IsDeleted),
                _ => query
            };
        }

        if (filter.Rating.HasValue)
            query = query.Where(r => r.Rating == filter.Rating.Value);

        if (filter.IsHidden.HasValue)
            query = query.Where(r => r.IsHidden == filter.IsHidden.Value);

        if (filter.IsDeleted.HasValue)
            query = query.Where(r => r.IsDeleted == filter.IsDeleted.Value);

        if (filter.IsVerifiedPurchase.HasValue)
            query = query.Where(r => r.IsVerifiedPurchase == filter.IsVerifiedPurchase.Value);

        if (filter.HasReports == true)
            query = query.Where(r => r.ReportCount > 0);

        // Search
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(r => 
                (r.Comment != null && r.Comment.ToLower().Contains(searchTerm)) ||
                r.User.Name.ToLower().Contains(searchTerm) ||
                r.Product.Name.ToLower().Contains(searchTerm));
        }

        // Date range
        if (filter.FromDate.HasValue)
            query = query.Where(r => r.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(r => r.CreatedAt <= filter.ToDate.Value);

        // Sort
        query = filter.SortBy switch
        {
            ReviewAdminSortBy.Newest => query.OrderByDescending(r => r.CreatedAt),
            ReviewAdminSortBy.Oldest => query.OrderBy(r => r.CreatedAt),
            ReviewAdminSortBy.HighestRating => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            ReviewAdminSortBy.LowestRating => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            ReviewAdminSortBy.MostReported => query.OrderByDescending(r => r.ReportCount).ThenByDescending(r => r.CreatedAt),
            ReviewAdminSortBy.MostHelpful => query.OrderByDescending(r => r.HelpfulCount).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<Review>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<bool> HasUserReviewedProductAsync(int userId, int productId)
    {
        return await _dbSet.AnyAsync(r => r.UserId == userId && r.ProductId == productId && !r.IsDeleted);
    }

    public async Task<int> CountUserReviewsInPeriodAsync(int userId, DateTime fromDate)
    {
        return await _dbSet.CountAsync(r => r.UserId == userId && r.CreatedAt >= fromDate);
    }

    public async Task<List<Review>> GetPendingReviewsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Product)
            .Where(r => r.Status == ReviewStatus.Pending && !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Review>> GetReportedReviewsAsync(int minReportCount = 1)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Product)
            .Where(r => r.ReportCount >= minReportCount && !r.IsDeleted)
            .OrderByDescending(r => r.ReportCount)
            .ToListAsync();
    }
}
