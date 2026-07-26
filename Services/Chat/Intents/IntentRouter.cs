using System.Text.RegularExpressions;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Fruitables.Services.Chat.Intents;

// Phân loại intent bằng rule-based (không gọi LLM) → nhanh, không timeout.
public sealed class IntentRouter : IIntentRouter
{
    private readonly ILogger<IntentRouter> _logger;

    // Keywords cho từng intent
    private static readonly Dictionary<ChatIntentKind, string[]> IntentKeywords = new()
    {
        [ChatIntentKind.OrderStatus] = new[]
        {
            "đơn hàng", "don hang", "order", "tra cứu", "tra cuu", "kiểm tra đơn",
            "kiem tra don", "trạng thái", "trang thai", "giao hàng", "giao hang",
            "vận chuyển", "van chuyen", "shipment", "tracking", "mã đơn", "ma don"
        },
        [ChatIntentKind.ProductLookup] = new[]
        {
            "sản phẩm", "san pham", "product", "tìm kiếm", "tim kiem", "search",
            "có bán", "co ban", "còn hàng", "con hang", "hết hàng", "het hang",
            "giá", "gia", "price", "mua", "trái cây", "trai cay", "fruit",
            "táo", "tao", "cam", "xoài", "xoai", "chuối", "chuoi", "nho",
            "dưa hấu", "dua hau", "thanh long", "mãng cầu", "mang cau"
        },
        [ChatIntentKind.CouponCheck] = new[]
        {
            "mã giảm giá", "ma giam gia", "coupon", "discount", "khuyến mãi",
            "khuyen mai", "giảm giá", "giam gia", "promo", "voucher", "mã giảm",
            "ma giam"
        },
        [ChatIntentKind.ShippingQuote] = new[]
        {
            "phí ship", "phi ship", "shipping", "phí vận chuyển", "phi van chuyen",
            "ship bao nhiêu", "ship bao nhieu", "giao hàng bao lâu", "giao hang bao lau",
            "phí giao", "phi giao", "delivery fee", "ship cost"
        }
    };

    // OutOfScope patterns
    private static readonly string[] OutOfScopePatterns = new[]
    {
        "admin", "quản trị", "mat khau", "password", "api key", "connection string",
        "debug", "config", "secret", "token", "credential", "database",
        "ignore previous", "system prompt", "developer mode", "dan", "jailbreak"
    };

    public IntentRouter(ILogger<IntentRouter> logger)
    {
        _logger = logger;
    }

    public Task<ChatIntent> ClassifyAsync(string userMessage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var message = (userMessage ?? string.Empty).ToLowerInvariant().Trim();

        // OutOfScope check
        if (IsOutOfScope(message))
        {
            _logger.LogInformation("Intent: OutOfScope (prompt injection or admin query)");
            return Task.FromResult(ChatIntent.Of(ChatIntentKind.OutOfScope, 0.95f));
        }

        // Score each intent
        var scores = new Dictionary<ChatIntentKind, float>();
        foreach (var (kind, keywords) in IntentKeywords)
        {
            var score = CalculateScore(message, keywords);
            if (score > 0)
                scores[kind] = score;
        }

        // Find best match
        if (scores.Count > 0)
        {
            var best = scores.OrderByDescending(x => x.Value).First();
            var slots = ExtractSlots(message, best.Key);

            _logger.LogInformation("Intent: {Kind} (confidence={Confidence})", best.Key, best.Value);

            return Task.FromResult(new ChatIntent
            {
                Kind = best.Key,
                Confidence = best.Value,
                Slots = slots
            });
        }

        // Default: GeneralInquiry
        _logger.LogInformation("Intent: GeneralInquiry (no keyword match)");
        return Task.FromResult(ChatIntent.Of(ChatIntentKind.GeneralInquiry, 0.5f));
    }

    private static float CalculateScore(string message, string[] keywords)
    {
        var matchCount = 0;
        foreach (var keyword in keywords)
        {
            if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                matchCount++;
        }

        if (matchCount == 0) return 0;

        // Normalize: more matches = higher confidence, capped at 0.95
        return Math.Min(0.95f, 0.6f + matchCount * 0.1f);
    }

    private static bool IsOutOfScope(string message)
    {
        return OutOfScopePatterns.Any(p => message.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> ExtractSlots(string message, ChatIntentKind kind)
    {
        var slots = new Dictionary<string, string>();

        switch (kind)
        {
            case ChatIntentKind.OrderStatus:
                // Extract order ID (e.g., "đơn hàng #123", "mã đơn 456")
                var orderMatch = Regex.Match(message, @"(?:#|đơn hàng|mã đơn|order)\s*(\d+)", RegexOptions.IgnoreCase);
                if (orderMatch.Success)
                    slots["orderId"] = orderMatch.Groups[1].Value;
                break;

            case ChatIntentKind.ProductLookup:
                // Extract product query (everything after "tìm", "tìm kiếm", "sản phẩm")
                var queryMatch = Regex.Match(message, @"(?:tìm|tìm kiếm|sản phẩm|search)\s+(.+?)(?:\?|$)", RegexOptions.IgnoreCase);
                if (queryMatch.Success)
                    slots["query"] = queryMatch.Groups[1].Value.Trim();
                else
                    slots["query"] = message; // Use entire message as query
                break;

            case ChatIntentKind.CouponCheck:
                // Extract coupon code (e.g., "mã GIAMGIA10", "coupon ABC123")
                var codeMatch = Regex.Match(message, @"(?:mã|code|coupon)\s+([A-Z0-9]+)", RegexOptions.IgnoreCase);
                if (codeMatch.Success)
                    slots["code"] = codeMatch.Groups[1].Value;
                break;

            case ChatIntentKind.ShippingQuote:
                // Extract address if mentioned
                var addrMatch = Regex.Match(message, @"(?:đến|tới|ship đến|giao đến)\s+(.+?)(?:\?|$)", RegexOptions.IgnoreCase);
                if (addrMatch.Success)
                    slots["address"] = addrMatch.Groups[1].Value.Trim();
                break;
        }

        return slots;
    }
}
