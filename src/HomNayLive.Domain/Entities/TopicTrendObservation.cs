using HomNayLive.Domain.Enums;

namespace HomNayLive.Domain.Entities;

public class TopicTrendObservation
{
    public long Id { get; set; }
    public long TopicId { get; set; }
    public string ProviderKind { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public WindowType WindowType { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public int? Rank { get; set; }
    public decimal? Score { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Topic Topic { get; set; } = null!;
}
