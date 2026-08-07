namespace Fruitables.Models;

public class ProductResult
{
    public bool Success { get; private set; }
    public Product? Product { get; private set; }
    public ProductErrorType? ErrorType { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ProductResult() { }

    public static ProductResult Ok(Product product)
    {
        return new ProductResult
        {
            Success = true,
            Product = product
        };
    }

    public static ProductResult Fail(ProductErrorType errorType, string message)
    {
        return new ProductResult
        {
            Success = false,
            ErrorType = errorType,
            ErrorMessage = message
        };
    }
}

public enum ProductErrorType
{
    NotFound,
    ValidationError,
    DuplicateSlug,
    DuplicateSKU,
    InvalidCategory,
    InvalidFileType,
    FileTooLarge,
    ImageNotFound,
    HasOrders
}
