using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Communications;
using Fruitables.Services.Identity.Rbac;
using Fruitables.Services.Infrastructure.Content;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fruitables.Tests;

public sealed class ContentAndAuditAggregateTests
{
    private static readonly VersionedJsonSerializer Serializer = new();

    [Fact]
    public async Task Faq_contact_and_testimonial_round_trip_content_entries()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);

        var faqService = new FaqService(
            db,
            new NoopIndexingService(),
            NullLogger<FaqService>.Instance,
            Serializer);
        var contactService = new ContactService(db, Serializer);
        var testimonialService = new TestimonialService(db, Serializer);

        var faq = await faqService.CreateAsync("Ship?", "30k", "shipping", true);
        var contact = await contactService.SendMessageAsync("An", "a@example.com", "hello");
        var testimonial = await testimonialService.AddTestimonialAsync(new Testimonial
        {
            Name = "Binh",
            Content = "Ngon",
            Rating = 5,
            IsActive = true
        });

        Assert.Equal(3, await db.ContentEntries.CountAsync());
        Assert.Equal("faq", (await db.ContentEntries.SingleAsync(e => e.Id == faq.Id)).EntryType);
        Assert.Equal("contact", (await db.ContentEntries.SingleAsync(e => e.Id == contact.Id)).EntryType);
        Assert.Equal("testimonial", (await db.ContentEntries.SingleAsync(e => e.Id == testimonial.Id)).EntryType);

        // Active services no longer write legacy content tables.
        Assert.False(await db.ContactMessages.AnyAsync());
        Assert.False(await db.Testimonials.AnyAsync());
        Assert.False(await db.Faqs.AnyAsync(item => item.Title == "Ship?"));
    }

    [Fact]
    public async Task Rbac_permission_resolution_uses_role_and_user_json()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        RbacTestHelper.SeedAdminUser(db, 1001);
        RbacTestHelper.SeedCustomerUser(db, 1100);
        RbacTestHelper.SeedActiveRole(db, 10, "Editor");
        RbacTestHelper.SeedPermission(db, 1, "orders.refund", "orders");
        await db.SaveChangesAsync();

        var svc = RbacTestHelper.CreateService(db);
        await svc.AssignPermissionToRoleAsync(10, 1, 1001);
        await svc.AssignRoleToUserAsync(1100, 10, 1001);

        Assert.True(await svc.HasPermissionAsync(1100, "orders.refund"));

        var user = await db.Users.SingleAsync(item => item.Id == 1100);
        var roles = Serializer.Deserialize<UserRolesDocument>(user.RoleIdsJson);
        Assert.Contains(roles.Roles, role => role.RoleId == 10);

        var role = await db.Roles.SingleAsync(item => item.Id == 10);
        var permissions = Serializer.Deserialize<RolePermissionsDocument>(role.PermissionsJson);
        Assert.Contains(permissions.Permissions, item => item.PermissionName == "orders.refund");

        Assert.True(await db.AuditLogs.AnyAsync());
        Assert.True(await db.RbacAuditLogs.AnyAsync());
    }

    private sealed class NoopIndexingService : IIndexingService
    {
        public Task IndexFaqAsync(int faqId, CancellationToken ct = default) => Task.CompletedTask;
        public Task IndexProductAsync(int productId, CancellationToken ct = default) => Task.CompletedTask;
        public Task IndexAllowlistedSettingsAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task IndexCatalogInsightsAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task IndexProductReviewSummaryAsync(int productId, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReindexAllAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
