using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Catalog.Products;

public interface IProductService
{
    Task<List<Product>> GetAllProductsAsync();
    Task<List<Product>> GetFeaturedProductsAsync(int count = 8);
    Task<List<Product>> GetProductsByCategoryAsync(int categoryId);
    Task<Product?> GetProductByIdAsync(int id);
    Task<Product?> GetProductBySlugAsync(string slug);
    Task<ShopViewModel> GetShopViewModelAsync(int? categoryId, string? search, decimal? minPrice, decimal? maxPrice, string? sortBy, int page, int pageSize);
    Task<List<Product>> GetRelatedProductsAsync(int productId, int count = 4);
}
