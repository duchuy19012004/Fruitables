using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public class Address
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ và tên"), MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), MaxLength(20)]
    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Số điện thoại phải có 10-11 chữ số")]
    public string Phone { get; set; } = string.Empty;

    // Structured address fields
    [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố"), MaxLength(20)]
    public string ProvinceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố"), MaxLength(100)]
    public string ProvinceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn Phường/Xã"), MaxLength(20)]
    public string CommuneCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn Phường/Xã"), MaxLength(100)]
    public string CommuneName { get; set; } = string.Empty;

    public int? GhnDistrictId { get; set; }

    [MaxLength(20)]
    public string? GhnWardCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số nhà, tên đường"), MaxLength(200)]
    public string StreetAddress { get; set; } = string.Empty;

    [NotMapped]
    public string FullAddress => $"{StreetAddress}, {CommuneName}, {ProvinceName}";

    public bool IsDefault { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
