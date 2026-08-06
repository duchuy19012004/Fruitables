using System.ComponentModel.DataAnnotations;
using Fruitables.Models.Returns;

namespace Fruitables.Models;

public enum UserRole
{
    Customer,
    Admin,
    SuperAdmin
}

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Avatar { get; set; }

    public UserRole Role { get; set; } = UserRole.Customer;

    public string RoleIdsJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    [MaxLength(255)]
    public string? GoogleId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }

    // Password Reset fields
    [MaxLength(255)]
    public string? ResetPasswordToken { get; set; }

    public DateTime? ResetPasswordTokenExpiresAt { get; set; }

    // Lock info fields
    /// <summary>
    /// Loại khóa hiện tại (null nếu không bị khóa)
    /// </summary>
    public LockType? CurrentLockType { get; set; }

    /// <summary>
    /// Lý do khóa tài khoản
    /// </summary>
    [MaxLength(1000)]
    public string? LockReason { get; set; }

    /// <summary>
    /// Loại vi phạm dẫn đến khóa
    /// </summary>
    [MaxLength(200)]
    public string? LockViolationType { get; set; }

    /// <summary>
    /// Thời gian bị khóa
    /// </summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>
    /// Thời gian hết hạn khóa (null nếu khóa vĩnh viễn)
    /// </summary>
    public DateTime? LockExpiresAt { get; set; }

    /// <summary>
    /// ID của admin đã khóa tài khoản
    /// </summary>
    public int? LockedByAdminId { get; set; }

    // Navigation properties
    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public string WishlistJson { get; set; } = "[]";
    public virtual Cart? Cart { get; set; }
    
    /// <summary>
    /// Admin đã khóa tài khoản này
    /// </summary>
    public virtual User? LockedByAdmin { get; set; }
    
    /// <summary>
    /// Lịch sử khóa/mở khóa tài khoản
    /// </summary>
    public virtual ICollection<UserAccountLog> AccountLogs { get; set; } = new List<UserAccountLog>();
    public virtual ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();
    public virtual ICollection<ReturnCase> ReturnCases { get; set; } = new List<ReturnCase>();
    
    /// <summary>
    /// Vai trò RBAC của người dùng
    /// </summary>
    public virtual ICollection<UserRoleMapping> UserRoleMappings { get; set; } = new List<UserRoleMapping>();
}
