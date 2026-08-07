using Fruitables.Data;
using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Services.Communications;

namespace Fruitables.Services.Reviews;

public class TestimonialService : ITestimonialService
{
    private readonly ApplicationDbContext _db;

    public TestimonialService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Testimonial>> GetActiveTestimonialsAsync()
    {
        return await _db.Testimonials
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Testimonial> AddTestimonialAsync(Testimonial testimonial)
    {
        await _db.Testimonials.AddAsync(testimonial);
        await _db.SaveChangesAsync();
        return testimonial;
    }

    public async Task<List<Testimonial>> GetAllAsync()
    {
        return await _db.Testimonials
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // Tạo đề xuất testimonial từ review tích cực (IsActive=false, chờ admin duyệt).
    public async Task<Testimonial?> SuggestFromReviewAsync(int reviewId)
    {
        var review = await _db.Reviews
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted && !r.IsHidden);

        // Chỉ đề xuất review rõ ràng tích cực: 4-5 sao + có comment + có nhãn tích cực
        if (review is null || review.Rating < 4 || string.IsNullOrWhiteSpace(review.Comment))
            return null;

        var sentiment = await _db.ReviewSentiments
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

        await _db.Testimonials.AddAsync(testimonial);
        await _db.SaveChangesAsync();
        return testimonial;
    }

    public async Task<bool> SetActiveAsync(int id, bool active)
    {
        var testimonial = await _db.Testimonials.FindAsync(id);
        if (testimonial is null) return false;

        testimonial.IsActive = active;
        _db.Testimonials.Update(testimonial);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var testimonial = await _db.Testimonials.FindAsync(id);
        if (testimonial is null) return false;

        _db.Testimonials.Remove(testimonial);
        await _db.SaveChangesAsync();
        return true;
    }
}
