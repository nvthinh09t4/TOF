using HomNayLive.Providers.Contracts;
using HomNayLive.Providers.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace HomNayLive.Providers.Tests;

public class ProviderFailureHandlingTests
{
    [Fact]
    public async Task RetryingTrendProviderHonorsTimeoutAndReturnsFailureEnvelope()
    {
        var provider = new RetryingTrendProvider(
            new SlowTrendProvider(),
            new ProviderExecutionOptions(TimeSpan.FromMilliseconds(100), 2, TimeSpan.FromMilliseconds(10)),
            new ProviderExecutionPolicy());

        var result = await provider.FetchAsync(new TrendProviderRequest(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, 10));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProviderErrorCategory.UnavailableTimeout, result.Failure!.Category);
    }

    [Fact]
    public async Task RetryingProviderHonorsCancellation()
    {
        var provider = new RetryingTrendProvider(
            new SlowTrendProvider(),
            new ProviderExecutionOptions(TimeSpan.FromSeconds(1), 5, TimeSpan.FromMilliseconds(10)),
            new ProviderExecutionPolicy());

        using var cts = new CancellationTokenSource();
        var task = provider.FetchAsync(new TrendProviderRequest(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, 10), cts.Token);

        cts.CancelAfter(50);
        var result = await task;

        Assert.False(result.IsSuccess);
        Assert.Equal(ProviderErrorCategory.Cancelled, result.Failure!.Category);
    }

    private sealed class SlowTrendProvider : ITrendProvider
    {
        public async Task<ProviderResultEnvelope<TrendObservationCandidate>> FetchAsync(
            TrendProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            return ProviderResultEnvelope<TrendObservationCandidate>.Success(
                new[]
                {
                    new TrendObservationCandidate(
                        "Slow topic",
                        "slow-topic",
                        1,
                        10m,
                        DateTimeOffset.UtcNow,
                        new ProviderIdentity("trend", "slow", "topic-trend"))
                },
                new ProviderIdentity("trend", "slow", "topic-trend"),
                1,
                TimeSpan.FromSeconds(5));
        }
    }
}
