using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class Setting
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    [MaxLength(50)]
    public string? Group { get; set; }
}
