using HomNayLive.Providers.Contracts;

namespace HomNayLive.Providers;

public sealed class ProviderRegistrationOptions
{
    public bool EnableTrendProvider { get; set; } = true;
    public bool EnableNewsProvider { get; set; } = true;
    public bool EnableAiSummaryProvider { get; set; } = true;
}

public sealed class DeterministicTrendProvider : ITrendProvider
{
    private static readonly ProviderIdentity Provider = new("trend", "google-trends", "topic-trend", "default");

    public Task<ProviderResultEnvelope<TrendObservationCandidate>> FetchAsync(
        TrendProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new[]
        {
            new TrendObservationCandidate(
                "Artificial Intelligence",
                "artificial-intelligence",
                1,
                92.4m,
                DateTimeOffset.UtcNow,
                Provider,
                new Dictionary<string, object> { ["window"] = request.WindowStartUtc.ToString("O") }),
            new TrendObservationCandidate(
                "Machine Learning",
                "machine-learning",
                2,
                84.8m,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                Provider,
                new Dictionary<string, object> { ["source"] = "google-trends" })
        };

        return Task.FromResult(ProviderResultEnvelope<TrendObservationCandidate>.Success(
            items.Take(request.MaxResults).ToArray(),
            Provider,
            1,
            TimeSpan.FromMilliseconds(250)));
    }
}

public sealed class DeterministicNewsProvider : INewsProvider
{
    private static readonly ProviderIdentity Provider = new("news", "rss-aggregate", "article-ingestion", "default");

    public Task<ProviderResultEnvelope<NewsArticleCandidate>> FetchAsync(
        NewsProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new[]
        {
            new NewsArticleCandidate(
                "https://example.com/ai-brief",
                "AI investment accelerates",
                "Major market participants continue to expand AI programs.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "hash-ai-brief",
                Provider,
                new Dictionary<string, object> { ["sourceKey"] = request.SourceKey }),
            new NewsArticleCandidate(
                "https://example.com/ml-notes",
                "Machine learning teams deepen infrastructure work",
                "New demand for orchestration and automation is rising.",
                DateTimeOffset.UtcNow.AddHours(-5),
                "hash-ml-notes",
                Provider,
                new Dictionary<string, object> { ["sourceKey"] = request.SourceKey })
        };

        return Task.FromResult(ProviderResultEnvelope<NewsArticleCandidate>.Success(
            items.Take(request.MaxResults).ToArray(),
            Provider,
            1,
            TimeSpan.FromMilliseconds(220)));
    }
}

public sealed class DeterministicAiSummaryProvider : IAiSummaryProvider
{
    private static readonly ProviderIdentity Provider = new("ai", "azure-openai", "summarization", "default");

    public Task<ProviderResultEnvelope<AiSummaryResult>> SummarizeAsync(
        AiSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.TopicName))
        {
            return Task.FromResult(ProviderResultEnvelope<AiSummaryResult>.CreateFailure(
                Provider,
                ProviderFailure.PermanentFailure("A topic name is required for summarization."),
                1,
                TimeSpan.Zero));
        }

        var result = new AiSummaryResult(
            $"{request.TopicName} is receiving sustained interest from publishers, product teams, and investors. The material shows a clear upward trend with strong supporting evidence.",
            Provider.Name,
            "gpt-4.1-mini",
            DateTimeOffset.UtcNow,
            request.GroundingUrls.ToArray(),
            new Dictionary<string, object> { ["language"] = request.PreferredLanguage ?? "en" });

        return Task.FromResult(ProviderResultEnvelope<AiSummaryResult>.Success(
            new[] { result },
            Provider,
            1,
            TimeSpan.FromMilliseconds(310)));
    }
}
