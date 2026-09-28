using HomNayLive.Domain.Enums;

namespace HomNayLive.Domain.Entities;

public class Source
{
    public long Id { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SourceState State { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ArticleSourceLink> ArticleSourceLinks { get; set; } = new List<ArticleSourceLink>();
}
