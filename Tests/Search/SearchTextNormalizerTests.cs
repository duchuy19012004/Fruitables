// Tests/Search/SearchTextNormalizerTests.cs
using Fruitables.Services.Search;
using Xunit;

namespace Fruitables.Tests.Search;

public class SearchTextNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Táo  Fuji  ", "tao fuji")]
    [InlineData("Đậu Hà Lan", "dau ha lan")]
    [InlineData("TRÁI CÂY", "trai cay")]
    [InlineData("ảãáàạăắằẵặâấầẫậ", "aaaaaaaaaaaaaaa")]
    public void Normalize_strips_diacritics_and_collapses_space(string? input, string expected)
    {
        Assert.Equal(expected, SearchTextNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_is_idempotent()
    {
        var once = SearchTextNormalizer.Normalize("Nho Mỹ");
        Assert.Equal(once, SearchTextNormalizer.Normalize(once));
    }
}
