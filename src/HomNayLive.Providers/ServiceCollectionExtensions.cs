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

        if (options.EnableTrendProvider)
        {
            services.AddSingleton<ITrendProvider, HomNayLive.Providers.DeterministicTrendProvider>();
        }

        if (options.EnableNewsProvider)
        {
            services.AddSingleton<INewsProvider, HomNayLive.Providers.DeterministicNewsProvider>();
        }

        if (options.EnableAiSummaryProvider)
        {
            services.AddSingleton<IAiSummaryProvider, HomNayLive.Providers.DeterministicAiSummaryProvider>();
        }

        return services;
    }
}
