using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;

namespace Fruitables.Services;

public class TestimonialService : ITestimonialService
{
    private readonly IUnitOfWork _unitOfWork;

    public TestimonialService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Testimonial>> GetActiveTestimonialsAsync()
    {
        return await _unitOfWork.Testimonials.Query()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Testimonial> AddTestimonialAsync(Testimonial testimonial)
    {
        await _unitOfWork.Testimonials.AddAsync(testimonial);
        await _unitOfWork.SaveChangesAsync();
        return testimonial;
    }

    public async Task<List<Testimonial>> GetAllAsync()
    {
        return await _unitOfWork.Testimonials.Query()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // Tạo đề xuất testimonial từ review tích cực (IsActive=false, chờ admin duyệt).
    public async Task<Testimonial?> SuggestFromReviewAsync(int reviewId)
    {
        var review = await _unitOfWork.Reviews.Query()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted && !r.IsHidden);

        // Chỉ đề xuất review rõ ràng tích cực: 4-5 sao + có comment + có nhãn tích cực
        if (review is null || review.Rating < 4 || string.IsNullOrWhiteSpace(review.Comment))
            return null;

        var sentiment = await _unitOfWork.ReviewSentiments.Query()
            .FirstOrDefaultAsync(s => s.ReviewId == reviewId);
        if (sentiment is null
            || sentiment.Sentiment != SentimentLabel.Positive
            || sentiment.RatingSentiment != SentimentLabel.Positive
            || sentiment.CommentSentiment != SentimentLabel.Positive
            || sentiment.HasRatingCommentConflict
            || sentiment.NeedsManualReview)
            return null;

        var testimonial = new Testimonial
        {
            UserId = review.UserId,
            Name = review.User?.Name ?? "Khách hàng",
            Content = review.Comment,
            Rating = review.Rating,
            IsActive = false, // chờ admin duyệt
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Testimonials.AddAsync(testimonial);
        await _unitOfWork.SaveChangesAsync();
        return testimonial;
    }

    public async Task<bool> SetActiveAsync(int id, bool active)
    {
        var testimonial = await _unitOfWork.Testimonials.GetByIdAsync(id);
        if (testimonial is null) return false;

        testimonial.IsActive = active;
        _unitOfWork.Testimonials.Update(testimonial);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var testimonial = await _unitOfWork.Testimonials.GetByIdAsync(id);
        if (testimonial is null) return false;

        _unitOfWork.Testimonials.Remove(testimonial);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
