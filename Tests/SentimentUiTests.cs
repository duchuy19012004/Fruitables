using Xunit;

namespace Fruitables.Tests;

public sealed class SentimentUiTests
{
    [Fact]
    public void Dashboard_PrioritizesActionQueueAndKeepsOnlyTrendChart()
    {
        var view = ReadView("Index.cshtml");

        Assert.Contains("sent-action-queue", view);
        Assert.Contains("id=\"trendChart\"", view);
        Assert.DoesNotContain("id=\"distributionChart\"", view);
    }

    [Fact]
    public void Dashboard_ExposesCustomDateRangeControls()
    {
        var view = ReadView("Index.cshtml");

        Assert.Contains("sent-date-filter", view);
        Assert.Contains("name=\"from\"", view);
        Assert.Contains("name=\"to\"", view);
        Assert.Contains("type=\"date\"", view);
    }

    [Fact]
    public void Reviews_UsesCompactRowsAndInlineDetailPanel()
    {
        var view = ReadView("Reviews.cshtml");

        Assert.Contains("sent-review-list", view);
        Assert.Contains("sentimentDetail", view);
        Assert.Contains("data-review-id", view);
    }

    [Fact]
    public void SentimentViews_UseSemanticButtonColorRoles()
    {
        var dashboard = ReadView("Index.cshtml");
        var reviews = ReadView("Reviews.cshtml");

        Assert.Contains("sent-btn-primary", dashboard);
        Assert.Contains("sent-btn-neutral", dashboard);
        Assert.Contains("sent-btn-warning", dashboard);
        Assert.Contains("sent-btn-info", reviews);
        Assert.Contains("sent-btn-ai", reviews);
        Assert.Contains("sent-btn-danger", reviews);
    }

    private static string ReadView(string fileName)
        => File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../Areas/Admin/Views/Sentiment",
            fileName)));
}
