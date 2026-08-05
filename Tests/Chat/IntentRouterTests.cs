using Fruitables.Services.Chat.Intents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fruitables.Tests.Chat;

public class IntentRouterTests
{
    private readonly IntentRouter _sut = new(NullLogger<IntentRouter>.Instance);

    [Theory]
    [InlineData("chào", "greeting")]
    [InlineData("cảm ơn bạn", "thanks")]
    [InlineData("xin lỗi", "apology")]
    [InlineData("tạm biệt nhé", "goodbye")]
    [InlineData("ok", "acknowledgement")]
    [InlineData("bạn có thể giúp gì?", "capability")]
    public async Task ClassifyAsync_social_message_returns_small_talk(string message, string category)
    {
        var result = await _sut.ClassifyAsync(message);

        Assert.Equal(ChatIntentKind.SmallTalk, result.Kind);
        Assert.Equal(category, result.Slots["category"]);
    }

    [Fact]
    public async Task ClassifyAsync_social_prefix_with_shipping_question_keeps_shipping_intent()
    {
        var result = await _sut.ClassifyAsync("cảm ơn, phí ship bao nhiêu?");

        Assert.Equal(ChatIntentKind.ShippingQuote, result.Kind);
    }
}
