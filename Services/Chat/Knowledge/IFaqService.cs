using Fruitables.Models;

namespace Fruitables.Services.Chat.Knowledge;

// ============================================================
// Quản lý bài FAQ + tự "đưa vào sổ tri thức" của bot sau khi lưu.
// ============================================================
public interface IFaqService
{
    Task<List<Faq>> GetAllAsync(CancellationToken ct = default);
    Task<Faq?> GetByIdAsync(int id, CancellationToken ct = default);

    // Tạo / sửa FAQ rồi index để bot học ngay
    Task<Faq> CreateAsync(string title, string body, string category, bool isActive, CancellationToken ct = default);
    Task<Faq?> UpdateAsync(int id, string title, string body, string category, bool isActive, CancellationToken ct = default);

    // Bật/tắt FAQ (tắt thì bot không dùng nữa)
    Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default);

    // Đồng bộ lại toàn bộ tri thức (FAQ + SP + settings)
    Task ReindexAllAsync(CancellationToken ct = default);
}
