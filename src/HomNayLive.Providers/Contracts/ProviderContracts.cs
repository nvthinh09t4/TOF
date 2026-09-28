namespace HomNayLive.Providers.Contracts;

public enum ProviderErrorCategory
{
    UnavailableTimeout,
    TransientExhausted,
    PermanentValidation,
    AuthenticationConfiguration,
    Cancelled
}

public sealed record ProviderIdentity(
    string Kind,
    string Name,
    string Capability,
    string? InstanceKey = null);

public sealed record ProviderExecutionOptions(
    TimeSpan? Timeout = null,
    int MaxAttempts = 3,
    TimeSpan? RetryDelay = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(10);
    public TimeSpan EffectiveRetryDelay => RetryDelay ?? TimeSpan.FromSeconds(1);
}

public sealed record ProviderFailure(
    ProviderErrorCategory Category,
    string Message,
    int AttemptCount = 0,
    Exception? Exception = null)
{
    public string Code => Category.ToString();

    public static ProviderFailure TimeoutFailure(string message, int attemptCount = 1, Exception? exception = null) =>
        new(ProviderErrorCategory.UnavailableTimeout, message, attemptCount, exception);

    public static ProviderFailure TransientFailure(string message, int attemptCount = 1, Exception? exception = null) =>
        new(ProviderErrorCategory.TransientExhausted, message, attemptCount, exception);

    public static ProviderFailure PermanentFailure(string message, int attemptCount = 0, Exception? exception = null) =>
        new(ProviderErrorCategory.PermanentValidation, message, attemptCount, exception);

    public static ProviderFailure AuthenticationFailure(string message, int attemptCount = 0, Exception? exception = null) =>
        new(ProviderErrorCategory.AuthenticationConfiguration, message, attemptCount, exception);

    public static ProviderFailure CancelledFailure(string message, int attemptCount = 0, Exception? exception = null) =>
        new(ProviderErrorCategory.Cancelled, message, attemptCount, exception);
}

public sealed record ProviderResultEnvelope<T>(
    IReadOnlyList<T> Results,
    ProviderIdentity Provider,
    int Attempts,
    TimeSpan Duration,
    bool IsSuccess,
    ProviderFailure? Failure = null)
{
    public static ProviderResultEnvelope<T> Success(
        IReadOnlyList<T> results,
        ProviderIdentity provider,
        int attempts,
        TimeSpan duration)
    {
        return new(results, provider, attempts, duration, true, null);
    }

    public static ProviderResultEnvelope<T> CreateFailure(
        ProviderIdentity provider,
        ProviderFailure failure,
        int attempts,
        TimeSpan duration)
    {
        return new(Array.Empty<T>(), provider, attempts, duration, false, failure);
    }
}

public sealed record TrendProviderRequest(
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    int MaxResults = 25,
    ProviderExecutionOptions? Options = null);

public sealed record TrendObservationCandidate(
    string TopicName,
    string NormalizedTopicName,
    int? Rank,
    decimal? Score,
    DateTimeOffset ObservedAtUtc,
    ProviderIdentity Provider,
    IReadOnlyDictionary<string, object>? Metadata = null);

public interface ITrendProvider
{
    Task<ProviderResultEnvelope<TrendObservationCandidate>> FetchAsync(
        TrendProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record NewsProviderRequest(
    string SourceKey,
    string? Query = null,
    int MaxResults = 25,
    DateTimeOffset? SinceUtc = null,
    ProviderExecutionOptions? Options = null);

public sealed record NewsArticleCandidate(
    string CanonicalUrl,
    string Title,
    string? Excerpt,
    DateTimeOffset? PublishedAtUtc,
    string? ContentHash,
    ProviderIdentity Provider,
    IReadOnlyDictionary<string, object>? Metadata = null);

public interface INewsProvider
{
    Task<ProviderResultEnvelope<NewsArticleCandidate>> FetchAsync(
        NewsProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AiSummaryRequest(
    string TopicName,
    IReadOnlyList<string> SourceMaterial,
    IReadOnlyList<string> GroundingUrls,
    string? PreferredLanguage = null,
    ProviderExecutionOptions? Options = null);

public sealed record AiSummaryResult(
    string SummaryText,
    string ProviderKey,
    string ModelKey,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<string> GroundingUrls,
    IReadOnlyDictionary<string, object>? Metadata = null);

public interface IAiSummaryProvider
{
    Task<ProviderResultEnvelope<AiSummaryResult>> SummarizeAsync(
        AiSummaryRequest request,
        CancellationToken cancellationToken = default);
}
