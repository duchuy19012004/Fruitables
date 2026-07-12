using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.ViewModels;

public class FaqListViewModel
{
    public List<Faq> Faqs { get; set; } = new();
}

public class CreateFaqViewModel
{
    [Required(ErrorMessage = "Tiêu đề không được trống")]
    [MaxLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nội dung không được trống")]
    [Display(Name = "Nội dung")]
    public string Body { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Danh mục tối đa 50 ký tự")]
    [Display(Name = "Danh mục")]
    public string? Category { get; set; }

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;
}

public class EditFaqViewModel : CreateFaqViewModel
{
    public int Id { get; set; }
}
