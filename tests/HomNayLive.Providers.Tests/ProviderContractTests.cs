using HomNayLive.Providers.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HomNayLive.Providers.Tests;

public class ProviderContractTests
{
    [Fact]
    public async Task TrendProviderContractReturnsNeutralResultsWithoutCoreDependencyOnProviderTypes()
    {
        var services = new ServiceCollection();
        services.AddHomNayLiveProviders();

        using var provider = services.BuildServiceProvider();
        var trendProvider = provider.GetRequiredService<ITrendProvider>();

        var result = await trendProvider.FetchAsync(new TrendProviderRequest(
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            5));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Results);
        Assert.Equal("trend", result.Provider.Kind);
        Assert.All(result.Results, item => Assert.NotNull(item.TopicName));
    }

    [Fact]
    public async Task NewsProviderContractRetainsNeutralArticleCandidates()
    {
        var services = new ServiceCollection();
        services.AddHomNayLiveProviders();

        using var provider = services.BuildServiceProvider();
        var newsProvider = provider.GetRequiredService<INewsProvider>();

        var result = await newsProvider.FetchAsync(new NewsProviderRequest("source-alpha", "ai", 10));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Results);
        Assert.Equal("news", result.Provider.Kind);
        Assert.All(result.Results, item => Assert.False(string.IsNullOrWhiteSpace(item.CanonicalUrl)));
    }

    [Fact]
    public async Task AiSummaryProviderContractReturnsSummaryWithGroundingAndNeutralResultShape()
    {
        var services = new ServiceCollection();
        services.AddHomNayLiveProviders();

        using var provider = services.BuildServiceProvider();
        var aiProvider = provider.GetRequiredService<IAiSummaryProvider>();

        var result = await aiProvider.SummarizeAsync(new AiSummaryRequest(
            "Artificial Intelligence",
            new[] { "Summary text from source A", "Summary text from source B" },
            new[] { "https://example.com/article-1", "https://example.com/article-2" },
            "en"));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Results);
        Assert.Equal("ai", result.Provider.Kind);
        Assert.NotEmpty(result.Results[0].SummaryText);
        Assert.NotEmpty(result.Results[0].GroundingUrls);
    }

    [Fact]
    public void ProviderErrorTaxonomyListsCategorizedFailures()
    {
        var timeout = ProviderFailure.TimeoutFailure("Timed out");
        var transient = ProviderFailure.TransientFailure("Retry exhausted");
        var permanent = ProviderFailure.PermanentFailure("Bad request");
        var auth = ProviderFailure.AuthenticationFailure("Missing key");
        var cancelled = ProviderFailure.CancelledFailure("Stopped");

        Assert.Equal(ProviderErrorCategory.UnavailableTimeout, timeout.Category);
        Assert.Equal(ProviderErrorCategory.TransientExhausted, transient.Category);
        Assert.Equal(ProviderErrorCategory.PermanentValidation, permanent.Category);
        Assert.Equal(ProviderErrorCategory.AuthenticationConfiguration, auth.Category);
        Assert.Equal(ProviderErrorCategory.Cancelled, cancelled.Category);
    }
}
