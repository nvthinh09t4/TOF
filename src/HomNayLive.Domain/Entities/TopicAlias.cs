namespace HomNayLive.Domain.Entities;

public class TopicAlias
{
    public long Id { get; set; }
    public long TopicId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Topic Topic { get; set; } = null!;
}
