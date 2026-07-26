using Fruitables.Models;

namespace Fruitables.ViewModels;

public class ShopViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<ProductTag> Tags { get; set; } = new();
    public int? SelectedCategoryId { get; set; }
    public string? SearchTerm { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SortBy { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 9;
}

public class ProductDetailViewModel
{
    public Product Product { get; set; } = null!;
    public List<Product> RelatedProducts { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
}
