namespace Fruitables.Options;

public class SearchSuggestOptions
{
    public const string SectionName = "SearchSuggest";

    public int MinQueryLength { get; set; } = 2;
    public int MaxQueryLength { get; set; } = 50;
    public int MaxProducts { get; set; } = 5;
    public int MaxCategories { get; set; } = 3;
    public int MaxKeywords { get; set; } = 5;
    public int RateLimitPerMinute { get; set; } = 60;
}
