using Fruitables.Models;

namespace Fruitables.Services.Reviews;

public interface ITestimonialService
{
    Task<List<Testimonial>> GetActiveTestimonialsAsync();
    Task<Testimonial> AddTestimonialAsync(Testimonial testimonial);

    // Đề xuất testimonial từ review tích cực (chờ admin duyệt, IsActive=false)
    Task<Testimonial?> SuggestFromReviewAsync(int reviewId);

    // Testimonials chờ duyệt + đã active (admin)
    Task<List<Testimonial>> GetAllAsync();

    Task<bool> SetActiveAsync(int id, bool active);
    Task<bool> DeleteAsync(int id);
}
