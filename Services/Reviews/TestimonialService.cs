using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Infrastructure.Content;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Reviews;

public class TestimonialService : ITestimonialService
{
    private readonly ApplicationDbContext _db;
    private readonly IJsonDocumentSerializer _serializer;

    public TestimonialService(ApplicationDbContext db, IJsonDocumentSerializer? serializer = null)
    {
        _db = db;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    public TestimonialService(IUnitOfWork unitOfWork)
        : this(((Repositories.UnitOfWork)unitOfWork).Context)
    {
    }

    public async Task<List<Testimonial>> GetActiveTestimonialsAsync()
    {
        var entries = await _db.ContentEntries.AsNoTracking()
            .Where(entry => entry.EntryType == ContentEntryMapper.TestimonialType && entry.IsActive)
            .OrderByDescending(entry => entry.CreatedAt)
            .ToListAsync();
        return entries.Select(entry => ContentEntryMapper.ToTestimonial(entry, _serializer)).ToList();
    }

    public async Task<Testimonial> AddTestimonialAsync(Testimonial testimonial)
    {
        var entry = ContentEntryMapper.FromTestimonial(testimonial, _serializer);
        _db.ContentEntries.Add(entry);
        await _db.SaveChangesAsync();
        entry.Key = ContentEntryMapper.Key("testimonial", entry.Id);
        await _db.SaveChangesAsync();
        return ContentEntryMapper.ToTestimonial(entry, _serializer);
    }

    public async Task<List<Testimonial>> GetAllAsync()
    {
        var entries = await _db.ContentEntries.AsNoTracking()
            .Where(entry => entry.EntryType == ContentEntryMapper.TestimonialType)
            .OrderByDescending(entry => entry.CreatedAt)
            .ToListAsync();
        return entries.Select(entry => ContentEntryMapper.ToTestimonial(entry, _serializer)).ToList();
    }

    public async Task<Testimonial?> SuggestFromReviewAsync(int reviewId)
    {
        var review = await _db.Reviews
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == reviewId && !item.IsDeleted && !item.IsHidden);

        if (review is null || review.Rating < 4 || string.IsNullOrWhiteSpace(review.Comment))
            return null;

        var sentiment = await _db.ReviewSentiments.FirstOrDefaultAsync(item => item.ReviewId == reviewId);
        if (sentiment is null
            || sentiment.Sentiment != SentimentLabel.Positive
            || sentiment.RatingSentiment != SentimentLabel.Positive
            || sentiment.CommentSentiment != SentimentLabel.Positive
            || sentiment.HasRatingCommentConflict
            || sentiment.NeedsManualReview)
            return null;

        return await AddTestimonialAsync(new Testimonial
        {
            UserId = review.UserId,
            Name = review.User?.Name ?? "Khách hàng",
            Content = review.Comment,
            Rating = review.Rating,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<bool> SetActiveAsync(int id, bool active)
    {
        var entry = await _db.ContentEntries.FirstOrDefaultAsync(item =>
            item.Id == id && item.EntryType == ContentEntryMapper.TestimonialType);
        if (entry is null)
            return false;

        var model = ContentEntryMapper.ToTestimonial(entry, _serializer);
        model.IsActive = active;
        ContentEntryMapper.FromTestimonial(model, _serializer, entry);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entry = await _db.ContentEntries.FirstOrDefaultAsync(item =>
            item.Id == id && item.EntryType == ContentEntryMapper.TestimonialType);
        if (entry is null)
            return false;
        _db.ContentEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }
}
