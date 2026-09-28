using HomNayLive.Providers.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static class HomNayLiveProviderServiceCollectionExtensions
{
    public static IServiceCollection AddHomNayLiveProviders(
        this IServiceCollection services,
        Action<HomNayLive.Providers.ProviderRegistrationOptions>? configure = null)
    {
        var options = new HomNayLive.Providers.ProviderRegistrationOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<HomNayLive.Providers.Execution.ProviderExecutionPolicy>();

        if (options.EnableTrendProvider)
        {
            services.AddSingleton<HomNayLive.Providers.DeterministicTrendProvider>();
            services.AddSingleton<ITrendProvider>(sp => new HomNayLive.Providers.Execution.RetryingTrendProvider(
                sp.GetRequiredService<HomNayLive.Providers.DeterministicTrendProvider>(),
                options.TrendExecutionOptions,
                sp.GetRequiredService<HomNayLive.Providers.Execution.ProviderExecutionPolicy>()));
        }

        if (options.EnableNewsProvider)
        {
            services.AddSingleton<HomNayLive.Providers.DeterministicNewsProvider>();
            services.AddSingleton<INewsProvider>(sp => new HomNayLive.Providers.Execution.RetryingNewsProvider(
                sp.GetRequiredService<HomNayLive.Providers.DeterministicNewsProvider>(),
                options.NewsExecutionOptions,
                sp.GetRequiredService<HomNayLive.Providers.Execution.ProviderExecutionPolicy>()));
        }

        if (options.EnableAiSummaryProvider)
        {
            services.AddSingleton<HomNayLive.Providers.DeterministicAiSummaryProvider>();
            services.AddSingleton<IAiSummaryProvider>(sp => new HomNayLive.Providers.Execution.RetryingAiSummaryProvider(
                sp.GetRequiredService<HomNayLive.Providers.DeterministicAiSummaryProvider>(),
                options.AiExecutionOptions,
                sp.GetRequiredService<HomNayLive.Providers.Execution.ProviderExecutionPolicy>()));
        }

        return services;
    }
}
