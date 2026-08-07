namespace Fruitables.Models
{
    /// <summary>
    /// Bảng nối giữa User và Role trong hệ thống RBAC
    /// </summary>
    public class UserRoleMapping
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        public int RoleId { get; set; }
        
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public int? AssignedByAdminId { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Role Role { get; set; } = null!;
        public virtual User? AssignedByAdmin { get; set; }
    }
}
