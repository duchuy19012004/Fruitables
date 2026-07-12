// Services/Search/SearchSuggestRateLimitException.cs
namespace Fruitables.Services.Search;

public sealed class SearchSuggestRateLimitException : Exception
{
    public SearchSuggestRateLimitException(string message) : base(message) { }
}
