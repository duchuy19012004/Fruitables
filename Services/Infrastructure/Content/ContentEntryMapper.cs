using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Services.Infrastructure.Json;
using System.Text.Json;

namespace Fruitables.Services.Infrastructure.Content;

public static class ContentEntryMapper
{
    public const string FaqType = "faq";
    public const string TestimonialType = "testimonial";
    public const string ContactType = "contact";
    public const string HotKeywordType = "search-hot-keyword";

    public static ContentPayload ReadPayload(string json, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() is "[]" or "{}")
            return new ContentPayload { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        return serializer.Deserialize<ContentPayload>(json);
    }

    public static string SerializePayload(ContentPayload payload, IJsonDocumentSerializer serializer) =>
        serializer.Serialize(payload);

    public static int ParseLegacyId(string key, string prefix)
    {
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(key[prefix.Length..], out var id))
            return id;
        return 0;
    }

    public static string Key(string prefix, int id) => $"{prefix}:{id}";

    public static Faq ToFaq(ContentEntry entry, IJsonDocumentSerializer serializer)
    {
        var payload = ReadPayload(entry.PayloadJson, serializer);
        return new Faq
        {
            Id = entry.Id,
            Title = entry.Title,
            Body = payload.Body,
            Category = payload.Category,
            IsActive = entry.IsActive,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
    }

    public static Testimonial ToTestimonial(ContentEntry entry, IJsonDocumentSerializer serializer)
    {
        var payload = ReadPayload(entry.PayloadJson, serializer);
        var meta = ReadMeta(payload.Body);
        return new Testimonial
        {
            Id = entry.Id,
            UserId = meta.UserId,
            Name = meta.Name ?? entry.Title,
            Profession = meta.Profession,
            Avatar = meta.Avatar,
            Content = meta.Content ?? payload.Body,
            Rating = meta.Rating ?? 5,
            IsActive = entry.IsActive,
            CreatedAt = entry.CreatedAt
        };
    }

    public static ContactMessage ToContact(ContentEntry entry, IJsonDocumentSerializer serializer)
    {
        var payload = ReadPayload(entry.PayloadJson, serializer);
        var meta = ReadMeta(payload.Body);
        return new ContactMessage
        {
            Id = entry.Id,
            Name = meta.Name ?? entry.Title,
            Email = meta.Email ?? entry.Key,
            Message = meta.Content ?? payload.Body,
            IsRead = entry.IsRead,
            CreatedAt = entry.CreatedAt
        };
    }

    public static SearchHotKeyword ToHotKeyword(ContentEntry entry, IJsonDocumentSerializer serializer)
    {
        var payload = ReadPayload(entry.PayloadJson, serializer);
        var meta = ReadMeta(payload.Body);
        return new SearchHotKeyword
        {
            Id = entry.Id,
            Text = entry.Title,
            NormalizedText = meta.NormalizedText ?? entry.Key,
            Weight = meta.Weight ?? 0,
            IsActive = entry.IsActive,
            CreatedAt = entry.CreatedAt
        };
    }

    public static ContentEntry FromFaq(Faq faq, IJsonDocumentSerializer serializer, ContentEntry? existing = null)
    {
        var now = DateTime.UtcNow;
        var entry = existing ?? new ContentEntry
        {
            EntryType = FaqType,
            Key = faq.Id > 0 ? Key("faq", faq.Id) : $"faq-temp:{Guid.NewGuid():N}",
            CreatedAt = faq.CreatedAt == default ? now : faq.CreatedAt
        };
        entry.Title = faq.Title;
        entry.IsActive = faq.IsActive;
        entry.UpdatedAt = now;
        entry.PayloadJson = SerializePayload(new ContentPayload
        {
            Title = faq.Title,
            Body = faq.Body,
            Category = string.IsNullOrWhiteSpace(faq.Category) ? "general" : faq.Category,
            IsActive = faq.IsActive,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = now
        }, serializer);
        entry.RowVersion = Guid.NewGuid().ToByteArray();
        return entry;
    }

    public static ContentEntry FromTestimonial(Testimonial item, IJsonDocumentSerializer serializer, ContentEntry? existing = null)
    {
        var now = DateTime.UtcNow;
        var entry = existing ?? new ContentEntry
        {
            EntryType = TestimonialType,
            Key = item.Id > 0 ? Key("testimonial", item.Id) : $"testimonial-temp:{Guid.NewGuid():N}",
            CreatedAt = item.CreatedAt == default ? now : item.CreatedAt
        };
        entry.Title = item.Name;
        entry.IsActive = item.IsActive;
        entry.UpdatedAt = now;
        entry.PayloadJson = SerializePayload(new ContentPayload
        {
            Title = item.Name,
            Body = WriteMeta(new ContentMeta
            {
                Name = item.Name,
                Profession = item.Profession,
                Avatar = item.Avatar,
                Content = item.Content,
                Rating = item.Rating,
                UserId = item.UserId
            }),
            Category = "testimonial",
            IsActive = item.IsActive,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = now
        }, serializer);
        entry.RowVersion = Guid.NewGuid().ToByteArray();
        return entry;
    }

    public static ContentEntry FromContact(ContactMessage item, IJsonDocumentSerializer serializer, ContentEntry? existing = null)
    {
        var now = DateTime.UtcNow;
        var entry = existing ?? new ContentEntry
        {
            EntryType = ContactType,
            Key = item.Id > 0 ? Key("contact", item.Id) : $"contact-temp:{Guid.NewGuid():N}",
            CreatedAt = item.CreatedAt == default ? now : item.CreatedAt
        };
        entry.Title = item.Name;
        entry.IsActive = true;
        entry.IsRead = item.IsRead;
        entry.UpdatedAt = now;
        entry.PayloadJson = SerializePayload(new ContentPayload
        {
            Title = item.Name,
            Body = WriteMeta(new ContentMeta
            {
                Name = item.Name,
                Email = item.Email,
                Content = item.Message
            }),
            Category = "contact",
            IsActive = true,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = now
        }, serializer);
        entry.RowVersion = Guid.NewGuid().ToByteArray();
        return entry;
    }

    public static ContentEntry FromHotKeyword(SearchHotKeyword item, IJsonDocumentSerializer serializer, ContentEntry? existing = null)
    {
        var now = DateTime.UtcNow;
        var entry = existing ?? new ContentEntry
        {
            EntryType = HotKeywordType,
            Key = item.NormalizedText,
            CreatedAt = item.CreatedAt == default ? now : item.CreatedAt
        };
        entry.Title = item.Text;
        entry.IsActive = item.IsActive;
        entry.UpdatedAt = now;
        entry.PayloadJson = SerializePayload(new ContentPayload
        {
            Title = item.Text,
            Body = WriteMeta(new ContentMeta
            {
                Content = item.Text,
                NormalizedText = item.NormalizedText,
                Weight = item.Weight
            }),
            Category = "search",
            IsActive = item.IsActive,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = now
        }, serializer);
        entry.RowVersion = Guid.NewGuid().ToByteArray();
        return entry;
    }

    private static ContentMeta ReadMeta(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new ContentMeta();
        if (body.TrimStart().StartsWith('{'))
        {
            try
            {
                return JsonSerializer.Deserialize<ContentMeta>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new ContentMeta { Content = body };
            }
            catch
            {
                return new ContentMeta { Content = body };
            }
        }

        return new ContentMeta { Content = body };
    }

    private static string WriteMeta(ContentMeta meta) =>
        JsonSerializer.Serialize(meta, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private sealed class ContentMeta
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Profession { get; set; }
        public string? Avatar { get; set; }
        public string? Content { get; set; }
        public string? NormalizedText { get; set; }
        public int? Rating { get; set; }
        public int? UserId { get; set; }
        public int? Weight { get; set; }
    }
}
