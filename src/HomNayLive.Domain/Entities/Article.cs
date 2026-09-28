using HomNayLive.Domain.Enums;

namespace HomNayLive.Domain.Entities;

public class Article
{
    public long Id { get; set; }
    public string CanonicalUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? ContentHash { get; set; }
    public DateTime? PublicationAtUtc { get; set; }
    public ArticleLifecycleState LifecycleState { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ArticleSourceLink> ArticleSourceLinks { get; set; } = new List<ArticleSourceLink>();
    public ICollection<TopicArticleLink> TopicArticleLinks { get; set; } = new List<TopicArticleLink>();
    public ICollection<AiSummarySourceLink> AiSummarySourceLinks { get; set; } = new List<AiSummarySourceLink>();
    public ICollection<AiSummary> Summaries { get; set; } = new List<AiSummary>();
}
