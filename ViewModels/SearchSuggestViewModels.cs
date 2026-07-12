namespace Fruitables.ViewModels;

public class SearchSuggestResponse
{
    public string Query { get; set; } = string.Empty;
    public List<SearchSuggestProductDto> Products { get; set; } = new();
    public List<SearchSuggestCategoryDto> Categories { get; set; } = new();
    public List<SearchSuggestKeywordDto> Keywords { get; set; } = new();
    public string ViewAllUrl { get; set; } = string.Empty;
}

public class SearchSuggestProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class SearchSuggestCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class SearchSuggestKeywordDto
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
