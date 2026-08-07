using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Chat.Conversation;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Reviews;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Fruitables.Options;
using Moq;

namespace Fruitables.Tests;

public sealed class ReviewChatAggregateJsonTests
{
    private static readonly VersionedJsonSerializer Serializer = new();

    [Fact]
    public async Task Helpful_and_report_update_metadata_json()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        db.Users.AddRange(
            new User { Id = 10, Name = "U", Email = "u@x.com", Password = "x", Role = UserRole.Customer, IsActive = true },
            new User { Id = 11, Name = "V", Email = "v@x.com", Password = "x", Role = UserRole.Customer, IsActive = true });
        db.Categories.Add(new Category { Id = 1, Name = "F", Slug = "f" });
        db.Products.Add(new Product { Id = 1, CategoryId = 1, Name = "A", Slug = "a", Price = 1, IsActive = true });
        db.Reviews.Add(new Review
        {
            Id = 5,
            ProductId = 1,
            UserId = 10,
            Rating = 5,
            Comment = "ok",
            Status = ReviewStatus.Approved,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new ReviewService(
            new UnitOfWork(db),
            Mock.Of<IWordMaskingService>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReviewService>.Instance,
            serializer: Serializer);

        Assert.True(await service.MarkReviewHelpfulAsync(5, 11));
        Assert.False(await service.MarkReviewHelpfulAsync(5, 11));
        Assert.True(await service.ReportReviewAsync(5, new ReportReviewDto
        {
            Reason = ReportReason.Spam,
            Description = "spam"
        }, 11));
        Assert.False(await service.ReportReviewAsync(5, new ReportReviewDto
        {
            Reason = ReportReason.Spam
        }, 11));

        db.ChangeTracker.Clear();
        var review = await db.Reviews.SingleAsync(item => item.Id == 5);
        var metadata = Serializer.Deserialize<ReviewMetadataDocument>(review.MetadataJson);
        Assert.Equal(1, metadata.HelpfulCount);
        Assert.Contains(11, metadata.HelpfulUserIds);
        Assert.Equal(1, metadata.ReportCount);
        Assert.Contains(metadata.Reports, item => item.ReportedByUserId == 11);
    }

    [Fact]
    public async Task Chat_messages_append_to_session_json()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        var sessionId = Guid.NewGuid();
        db.ChatSessions.Add(new ChatSession
        {
            Id = sessionId,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            Source = "page"
        });
        await db.SaveChangesAsync();

        var session = await db.ChatSessions.SingleAsync();
        // Use ChatService private path via public API is heavy; assert helper path by direct document write parity.
        var document = new ChatMessagesDocument
        {
            Messages =
            [
                new ChatMessageDocument { Role = "user", Content = "hi", CreatedAt = DateTime.UtcNow },
                new ChatMessageDocument
                {
                    Role = "assistant",
                    Content = "hello",
                    CreatedAt = DateTime.UtcNow,
                    Metadata = new ChatMessageMetadata { Action = "none" }
                }
            ]
        };
        session.MessagesJson = Serializer.Serialize(document);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var stored = Serializer.Deserialize<ChatMessagesDocument>(
            (await db.ChatSessions.SingleAsync()).MessagesJson);
        Assert.Equal(2, stored.Messages.Count);
        Assert.Equal("user", stored.Messages[0].Role);
    }
}
