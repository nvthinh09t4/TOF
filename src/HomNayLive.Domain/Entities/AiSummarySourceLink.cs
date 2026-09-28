namespace HomNayLive.Domain.Entities;

public class AiSummarySourceLink
{
    public long Id { get; set; }
    public long AiSummaryId { get; set; }
    public long ArticleId { get; set; }

    public AiSummary AiSummary { get; set; } = null!;
    public Article Article { get; set; } = null!;
}
