using Xunit;

namespace Fruitables.Tests;

public sealed class LegacyReferenceGuardTests
{
    private static readonly string[] LegacyDbSets =
    [
        "ProductImages", "ProductTags", "CartGroups", "CartItems", "OrderStatusHistories", "OrderNotes",
        "SePayTransactions", "Coupons", "Combos", "ComboItems", "PriceSchedules", "ReviewReports",
        "ReviewHelpfuls", "ReviewSentiments", "ReviewSentimentAspects", "ReturnRequests", "ReturnRequestItems",
        "ReturnEvidence", "ReturnEvents", "Refunds", "Wishlists", "Testimonials", "ContactMessages", "Faqs",
        "ChatMessages", "SearchHotKeywords", "ProductLogs", "UserAccountLogs", "ComboAuditLogs", "Permissions",
        "UserRoleMappings", "RolePermissions", "RbacAuditLogs"
    ];

    private static readonly string[] LegacyEntityTokens =
    [
        "IRepository<ProductImage>", "IRepository<ProductTag>", "IRepository<CartGroup>", "IRepository<CartItem>",
        "IRepository<ReviewReport>", "IRepository<ReviewHelpful>", "IRepository<ReviewSentiment>",
        "IRepository<ReviewSentimentAspect>", "IRepository<PriceSchedule>", "IRepository<Combo>",
        "IRepository<ComboItem>", "IRepository<Coupon>", "IRepository<Permission>",
        "IRepository<UserRoleMapping>", "IRepository<RolePermission>", "IReviewReportRepository", ".ReturnRequestItems"
    ];

    // SQLite/InMemory fixtures still exercise the expand-window compatibility path. The allowlist
    // keeps that path explicit and prevents a new production caller from silently reintroducing it.
    private static readonly HashSet<string> ExpandCompatibilityFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Repositories/Interfaces/IUnitOfWork.cs",
        "Repositories/UnitOfWork.cs",
        "Services/Chat/Conversation/ChatService.cs",
        "Services/Chat/Knowledge/IndexingService.cs",
        "Services/Catalog/Categories/CategoryService.cs",
        "Services/Catalog/Products/ProductAdminService.cs",
        "Services/Identity/Rbac/RbacService.cs",
        "Services/Identity/Users/UserManagementService.cs",
        "Services/Infrastructure/DatabaseConsolidation/DatabaseConsolidationService.cs",
        "Services/Infrastructure/MigrationService.cs",
        "Services/Orders/Cart/CartService.cs",
        "Services/Pricing/ProductPricing/PriceManagementService.cs",
        "Services/Reviews/ReviewService.cs",
        "Services/Reviews/TestimonialService.cs",
        "Services/Sentiment/SentimentAnalysisService.cs",
        "Services/Returns/ReturnService.cs",
        "Services/Analytics/Dashboard/DashboardService.cs",
        "Areas/Admin/Controllers/ComboController.cs",
        "Services/Catalog/Combos/ComboService.cs",
        "Services/Analytics/Sales/SalesAnalyticsService.cs",
        "Repositories/ReviewReportRepository.cs",
        "Repositories/Interfaces/IReviewReportRepository.cs"
    };

    [Fact]
    public void Legacy_data_access_is_limited_to_explicit_expand_window_files()
    {
        var root = FindRepositoryRoot();
        var productionRoots = new[] { "Services", "Repositories", "Controllers", "Areas", "ViewComponents" };
        var violations = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => productionRoots.Any(directory =>
                file.StartsWith(Path.Combine(root, directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(file =>
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                var text = File.ReadAllText(file);
                var dbSetHits = LegacyDbSets
                    .Where(name => System.Text.RegularExpressions.Regex.IsMatch(
                        text,
                        $@"\\b(?:_context|_dbContext|_db|_uow|_unitOfWork)\\.{name}\\b"));
                return dbSetHits.Concat(LegacyEntityTokens.Where(text.Contains))
                    .Select(token => $"{relative}: {token}");
            })
            .Where(hit => !ExpandCompatibilityFiles.Contains(hit[..hit.IndexOf(':')]))
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fruitables.csproj")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
