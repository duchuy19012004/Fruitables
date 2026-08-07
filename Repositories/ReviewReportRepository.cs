using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Repositories;

public class ReviewReportRepository : IReviewReportRepository
{
    private readonly DbSet<ReviewReport> _dbSet;

    public ReviewReportRepository(ApplicationDbContext context)
    {
        _dbSet = context.ReviewReports;
    }

    public async Task<List<ReviewReport>> GetReportsByReviewIdAsync(int reviewId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(rr => rr.ReportedByUser)
            .Include(rr => rr.HandledByAdmin)
            .Where(rr => rr.ReviewId == reviewId)
            .OrderByDescending(rr => rr.CreatedAt)
            .ToListAsync();
    }

    public async Task<PagedResult<ReviewReport>> GetPendingReportsAsync(ReportFilterDto filter)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(rr => rr.Review)
                .ThenInclude(r => r.User)
            .Include(rr => rr.Review)
                .ThenInclude(r => r.Product)
            .Include(rr => rr.ReportedByUser)
            .Where(rr => rr.Status == ReportStatus.Pending);

        return await ApplyFiltersAndPaginateAsync(query, filter);
    }

    public async Task<PagedResult<ReviewReport>> GetAllReportsAsync(ReportFilterDto filter)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(rr => rr.Review)
                .ThenInclude(r => r.User)
            .Include(rr => rr.Review)
                .ThenInclude(r => r.Product)
            .Include(rr => rr.ReportedByUser)
            .Include(rr => rr.HandledByAdmin)
            .AsQueryable();

        return await ApplyFiltersAndPaginateAsync(query, filter);
    }

    public async Task<bool> HasUserReportedReviewAsync(int userId, int reviewId)
    {
        return await _dbSet.AnyAsync(rr => rr.ReportedByUserId == userId && rr.ReviewId == reviewId);
    }

    public async Task<int> CountPendingReportsAsync()
    {
        return await _dbSet.CountAsync(rr => rr.Status == ReportStatus.Pending);
    }

    private async Task<PagedResult<ReviewReport>> ApplyFiltersAndPaginateAsync(
        IQueryable<ReviewReport> query, 
        ReportFilterDto filter)
    {
        // Filters
        if (filter.Status.HasValue)
            query = query.Where(rr => rr.Status == filter.Status.Value);

        if (filter.Reason.HasValue)
            query = query.Where(rr => rr.Reason == filter.Reason.Value);

        if (filter.ProductId.HasValue)
            query = query.Where(rr => rr.Review.ProductId == filter.ProductId.Value);

        if (filter.ReviewId.HasValue)
            query = query.Where(rr => rr.ReviewId == filter.ReviewId.Value);

        // Date range
        if (filter.FromDate.HasValue)
            query = query.Where(rr => rr.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(rr => rr.CreatedAt <= filter.ToDate.Value);

        // Sort
        query = filter.SortBy switch
        {
            ReportSortBy.Newest => query.OrderByDescending(rr => rr.CreatedAt),
            ReportSortBy.Oldest => query.OrderBy(rr => rr.CreatedAt),
            _ => query.OrderByDescending(rr => rr.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<ReviewReport>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
}
