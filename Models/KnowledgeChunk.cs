using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

/// <summary>
/// Indexed text fragment for RAG retrieval. Multiple rows per (SourceType, SourceId) are expected
/// when a source is split into several chunks; the composite index is non-unique.
/// </summary>
public class KnowledgeChunk
{
    public long Id { get; set; }

    public KnowledgeSourceType SourceType { get; set; }

    [Required, MaxLength(64)]
    public string SourceId { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Title { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public string EmbeddingJson { get; set; } = "[]";

    [Required, MaxLength(64)]
    public string ContentHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
