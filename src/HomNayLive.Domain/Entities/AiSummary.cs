namespace HomNayLive.Domain.Entities;

public class AiSummary
{
    public long Id { get; set; }
    public long? ScopeTopicId { get; set; }
    public long? ScopeArticleId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Topic? ScopeTopic { get; set; }
    public Article? ScopeArticle { get; set; }
    public ICollection<AiSummarySourceLink> GroundingLinks { get; set; } = new List<AiSummarySourceLink>();
}
