namespace HomNayLive.Domain.Entities;

public class ArticleSourceLink
{
    public long Id { get; set; }
    public long ArticleId { get; set; }
    public long SourceId { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public DateTime DiscoveredAtUtc { get; set; }
    public bool IsPrimary { get; set; }

    public Article Article { get; set; } = null!;
    public Source Source { get; set; } = null!;
}
