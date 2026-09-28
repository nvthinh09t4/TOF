namespace HomNayLive.Domain.Entities;

public class TopicArticleLink
{
    public long Id { get; set; }
    public long TopicId { get; set; }
    public long ArticleId { get; set; }
    public DateTime LinkedAtUtc { get; set; }

    public Topic Topic { get; set; } = null!;
    public Article Article { get; set; } = null!;
}
