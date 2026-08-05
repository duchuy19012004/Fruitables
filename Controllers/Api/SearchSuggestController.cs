// Controllers/Api/SearchSuggestController.cs
using Fruitables.Options;
using Fruitables.Services.Communications;
using Fruitables.Services.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Fruitables.Controllers.Api;

[ApiController]
[Route("api/search")]
public class SearchSuggestController : ControllerBase
{
    private readonly ISearchSuggestService _service;
    private readonly IMemoryCache _cache;
    private readonly SearchSuggestOptions _options;
    private readonly ILogger<SearchSuggestController> _logger;

    public SearchSuggestController(
        ISearchSuggestService service,
        IMemoryCache cache,
        IOptions<SearchSuggestOptions> options,
        ILogger<SearchSuggestController> logger)
    {
        _service = service;
        _cache = cache;
        _options = options?.Value ?? new SearchSuggestOptions();
        _logger = logger;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken ct)
    {
        try
        {
            EnforceRateLimit();
            var payload = await _service.SuggestAsync(q, ct);
            return Ok(payload);
        }
        catch (SearchSuggestRateLimitException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search suggest failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau."
            });
        }
    }

    private void EnforceRateLimit()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var bucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var key = $"search-suggest-rl:{ip}:{bucket}";

        var count = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return 0;
        });

        count++;
        _cache.Set(key, count, TimeSpan.FromMinutes(2));

        if (count > _options.RateLimitPerMinute)
        {
            throw new SearchSuggestRateLimitException(
                $"Rate limit exceeded. Maximum {_options.RateLimitPerMinute} requests per minute.");
        }
    }
}
