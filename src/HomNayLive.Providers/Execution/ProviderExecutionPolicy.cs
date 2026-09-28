using HomNayLive.Providers.Contracts;

namespace HomNayLive.Providers.Execution;

public sealed class ProviderExecutionPolicy
{
    public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    public bool IsTransientFailure(ProviderFailure? failure)
    {
        return failure is not null &&
               (failure.Category == ProviderErrorCategory.UnavailableTimeout ||
                failure.Category == ProviderErrorCategory.TransientExhausted);
    }
}

public sealed class RetryingTrendProvider : ITrendProvider
{
    private readonly ITrendProvider _inner;
    private readonly ProviderExecutionOptions _options;
    private readonly ProviderExecutionPolicy _policy;

    public RetryingTrendProvider(ITrendProvider inner, ProviderExecutionOptions options, ProviderExecutionPolicy policy)
    {
        _inner = inner;
        _options = options;
        _policy = policy;
    }

    public async Task<ProviderResultEnvelope<TrendObservationCandidate>> FetchAsync(
        TrendProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.EffectiveTimeout);

        try
        {
            while (true)
            {
                attempt++;

                try
                {
                    var result = await _inner.FetchAsync(request, timeoutCts.Token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        return result with { Attempts = attempt, Duration = result.Duration };
                    }

                    if (!_policy.IsTransientFailure(result.Failure) || attempt >= _options.MaxAttempts)
                    {
                        return result with { Attempts = attempt, Duration = result.Duration };
                    }

                    await _policy.DelayAsync(_options.EffectiveRetryDelay, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
                {
                    return ProviderResultEnvelope<TrendObservationCandidate>.CreateFailure(
                        new ProviderIdentity("trend", "retrying-trend", "topic-trend"),
                        ProviderFailure.TimeoutFailure("Trend provider timed out.", attempt),
                        attempt,
                        _options.EffectiveTimeout);
                }
                catch (OperationCanceledException)
                {
                    return ProviderResultEnvelope<TrendObservationCandidate>.CreateFailure(
                        new ProviderIdentity("trend", "retrying-trend", "topic-trend"),
                        ProviderFailure.CancelledFailure("Trend provider call was cancelled.", attempt),
                        attempt,
                        TimeSpan.Zero);
                }
                catch (Exception ex)
                {
                    if (attempt >= _options.MaxAttempts)
                    {
                        return ProviderResultEnvelope<TrendObservationCandidate>.CreateFailure(
                            new ProviderIdentity("trend", "retrying-trend", "topic-trend"),
                            ProviderFailure.PermanentFailure($"Trend provider failed: {ex.Message}", attempt, ex),
                            attempt,
                            TimeSpan.Zero);
                    }

                    await _policy.DelayAsync(_options.EffectiveRetryDelay, timeoutCts.Token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }
}

public sealed class RetryingNewsProvider : INewsProvider
{
    private readonly INewsProvider _inner;
    private readonly ProviderExecutionOptions _options;
    private readonly ProviderExecutionPolicy _policy;

    public RetryingNewsProvider(INewsProvider inner, ProviderExecutionOptions options, ProviderExecutionPolicy policy)
    {
        _inner = inner;
        _options = options;
        _policy = policy;
    }

    public async Task<ProviderResultEnvelope<NewsArticleCandidate>> FetchAsync(
        NewsProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.EffectiveTimeout);

        try
        {
            while (true)
            {
                attempt++;

                try
                {
                    var result = await _inner.FetchAsync(request, timeoutCts.Token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        return result with { Attempts = attempt, Duration = result.Duration };
                    }

                    if (!_policy.IsTransientFailure(result.Failure) || attempt >= _options.MaxAttempts)
                    {
                        return result with { Attempts = attempt, Duration = result.Duration };
                    }

                    await _policy.DelayAsync(_options.EffectiveRetryDelay, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
                {
                    return ProviderResultEnvelope<NewsArticleCandidate>.CreateFailure(
                        new ProviderIdentity("news", "retrying-news", "article-ingestion"),
                        ProviderFailure.TimeoutFailure("News provider timed out.", attempt),
                        attempt,
                        _options.EffectiveTimeout);
                }
                catch (OperationCanceledException)
                {
                    return ProviderResultEnvelope<NewsArticleCandidate>.CreateFailure(
                        new ProviderIdentity("news", "retrying-news", "article-ingestion"),
                        ProviderFailure.CancelledFailure("News provider call was cancelled.", attempt),
                        attempt,
                        TimeSpan.Zero);
                }
                catch (Exception ex)
                {
                    if (attempt >= _options.MaxAttempts)
                    {
                        return ProviderResultEnvelope<NewsArticleCandidate>.CreateFailure(
                            new ProviderIdentity("news", "retrying-news", "article-ingestion"),
                            ProviderFailure.PermanentFailure($"News provider failed: {ex.Message}", attempt, ex),
                            attempt,
                            TimeSpan.Zero);
                    }

                    await _policy.DelayAsync(_options.EffectiveRetryDelay, timeoutCts.Token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }
}

public sealed class RetryingAiSummaryProvider : IAiSummaryProvider
{
    private readonly IAiSummaryProvider _inner;
    private readonly ProviderExecutionOptions _options;
    private readonly ProviderExecutionPolicy _policy;

    public RetryingAiSummaryProvider(IAiSummaryProvider inner, ProviderExecutionOptions options, ProviderExecutionPolicy policy)
    {
        _inner = inner;
        _options = options;
        _policy = policy;
    }

    public async Task<ProviderResultEnvelope<AiSummaryResult>> SummarizeAsync(
        AiSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.EffectiveTimeout);

        try
        {
            while (true)
            {
                attempt++;

                try
                {
                    var result = await _inner.SummarizeAsync(request, timeoutCts.Token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        return result with { Attempts = attempt, Duration = result.Duration };
                    }

                    if (!_policy.IsTransientFailure(result.Failure) || attempt >= _options.MaxAttempts)
                    {
                        return result with { Attempts = attempt, Duration = result.Duration };
                    }

                    await _policy.DelayAsync(_options.EffectiveRetryDelay, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
                {
                    return ProviderResultEnvelope<AiSummaryResult>.CreateFailure(
                        new ProviderIdentity("ai", "retrying-ai", "summarization"),
                        ProviderFailure.TimeoutFailure("AI summary provider timed out.", attempt),
                        attempt,
                        _options.EffectiveTimeout);
                }
                catch (OperationCanceledException)
                {
                    return ProviderResultEnvelope<AiSummaryResult>.CreateFailure(
                        new ProviderIdentity("ai", "retrying-ai", "summarization"),
                        ProviderFailure.CancelledFailure("AI summary provider call was cancelled.", attempt),
                        attempt,
                        TimeSpan.Zero);
                }
                catch (Exception ex)
                {
                    if (attempt >= _options.MaxAttempts)
                    {
                        return ProviderResultEnvelope<AiSummaryResult>.CreateFailure(
                            new ProviderIdentity("ai", "retrying-ai", "summarization"),
                            ProviderFailure.PermanentFailure($"AI summary provider failed: {ex.Message}", attempt, ex),
                            attempt,
                            TimeSpan.Zero);
                    }

                    await _policy.DelayAsync(_options.EffectiveRetryDelay, timeoutCts.Token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }
}
