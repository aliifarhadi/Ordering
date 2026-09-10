using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public static class DeterministicProvidersActivation
    {
        public static IServiceCollection AddDeterministicProvidersWhenEnabled(
            this IServiceCollection services,
            IConfiguration configuration)
            => configuration.GetValue<bool>(DeterministicAdapterOptions.EnabledKey)
                ? services.AddDeterministicProviders()
                : services;
    }
}
