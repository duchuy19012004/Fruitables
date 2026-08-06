using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fruitables.Models.Json;

public sealed class ContentPayload : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["title", "body"];

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = "general";

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; } = true;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(!string.IsNullOrWhiteSpace(Title), "title");
        Require(!string.IsNullOrWhiteSpace(Body), "body");
        Require(CreatedAt != default, "createdAt");
        Require(UpdatedAt != default, "updatedAt");
    }

}
