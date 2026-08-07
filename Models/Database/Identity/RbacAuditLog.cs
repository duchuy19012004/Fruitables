using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models
{
    public class RbacAuditLog
    {
        public int Id { get; set; }
        
        [Required, MaxLength(50)]
        public string Action { get; set; } = string.Empty; // Create, Update, Delete, Assign, Revoke
        
        [Required, MaxLength(50)]
        public string EntityType { get; set; } = string.Empty; // Role, Permission, UserRole, RolePermission
        
        public int EntityId { get; set; }
        
        public int ChangedByAdminId { get; set; }
        
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(2000)]
        public string? OldValue { get; set; } // JSON serialized
        
        [MaxLength(2000)]
        public string? NewValue { get; set; } // JSON serialized
        
        // Navigation properties
        public virtual User ChangedByAdmin { get; set; } = null!;
    }
}
