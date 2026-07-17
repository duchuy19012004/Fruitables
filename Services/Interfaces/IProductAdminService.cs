using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface IProductAdminService
{
    // Product CRUD
    Task<ProductListResult> GetProductsAsync(ProductListRequest request);
    Task<Product?> GetProductByIdAsync(int id);
    Task<ProductResult> CreateProductAsync(CreateProductRequest request);
    Task<ProductResult> UpdateProductAsync(UpdateProductRequest request);
    Task<ProductResult> SoftDeleteProductAsync(int id);
    Task<ProductResult> RestoreProductAsync(int id);
    Task<ProductResult> HardDeleteProductAsync(int id);
    
    // Image Management
    Task<ProductResult> AddImagesAsync(int productId, List<IFormFile> files);
    Task<ProductResult> SetPrimaryImageAsync(int productId, int imageId);
    Task<ProductResult> DeleteImageAsync(int productId, int imageId);
    Task<ProductResult> ReorderImagesAsync(int productId, List<int> imageIds);
    
    // Tag Management
    Task<ProductResult> UpdateTagsAsync(int productId, List<string> tagNames);
    
    // Variant Management
    Task<ProductResult> AddVariantAsync(CreateVariantRequest request);
    Task<ProductResult> UpdateVariantAsync(int variantId, UpdateVariantRequest request);
    Task<ProductResult> DeleteVariantAsync(int variantId);
}