using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

/// <summary>
/// Log lịch sử khóa/mở khóa tài khoản người dùng
/// </summary>
public class UserAccountLog
{
    public int Id { get; set; }

    /// <summary>
    /// ID của người dùng bị khóa/mở khóa
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// ID của admin thực hiện hành động
    /// </summary>
    public int AdminId { get; set; }

    /// <summary>
    /// Hành động: "Lock" hoặc "Unlock"
    /// </summary>
    [Required, MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Loại khóa (chỉ có khi Action = "Lock")
    /// </summary>
    public LockType? LockType { get; set; }

    /// <summary>
    /// Loại vi phạm (chỉ có khi Action = "Lock")
    /// </summary>
    [MaxLength(200)]
    public string? ViolationType { get; set; }

    /// <summary>
    /// Lý do khóa/mở khóa
    /// </summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }

    /// <summary>
    /// Thời gian hết hạn khóa (chỉ có khi LockType = Temporary)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Thời gian thực hiện hành động
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Địa chỉ IP của admin thực hiện
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent của admin thực hiện
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual User Admin { get; set; } = null!;
}
