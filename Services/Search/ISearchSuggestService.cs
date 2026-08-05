// Services/Interfaces/ISearchSuggestService.cs
using Fruitables.ViewModels;

namespace Fruitables.Services.Search;

public interface ISearchSuggestService
{
    Task<SearchSuggestResponse> SuggestAsync(string? query, CancellationToken ct = default);
}
