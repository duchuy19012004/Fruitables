using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models
{
    public class Permission
    {
        public int Id { get; set; }
        
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Format: "module.action"
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Required, MaxLength(50)]
        public string Module { get; set; } = string.Empty; // e.g., "products", "orders"
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
