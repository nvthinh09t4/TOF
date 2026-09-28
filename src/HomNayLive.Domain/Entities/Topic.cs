namespace HomNayLive.Domain.Entities;

public class Topic
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<TopicAlias> TopicAliases { get; set; } = new List<TopicAlias>();
    public ICollection<TopicTrendObservation> TrendObservations { get; set; } = new List<TopicTrendObservation>();
    public ICollection<TopicArticleLink> TopicArticleLinks { get; set; } = new List<TopicArticleLink>();
    public ICollection<AiSummary> Summaries { get; set; } = new List<AiSummary>();
}
