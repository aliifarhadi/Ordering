using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.Providers.FlightFlow;
using AeroTech.Ordering.Domain.Providers.Payment;
using AeroTech.Ordering.Domain.Providers.Pricing;
using AeroTech.Ordering.Providers.FlightFlow.Services;
using AeroTech.Ordering.Providers.Offer.Services;
using AeroTech.Ordering.Providers.Payment.Services;
using AeroTech.Ordering.Providers.Payment.Options;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Providers.DocumentIssuance.Services;
using AeroTech.Ordering.Domain.Ports.Funding;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain.Ports.Reservation;
using AeroTech.Ordering.Providers.OrderChange.Services;
using AeroTech.Ordering.Providers.Pricing.Services;
using AeroTech.Ordering.Providers.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.Ordering.Providers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddProviders(this IServiceCollection services, IConfiguration configuration)
        {
            var offerBaseUrl = configuration["Offer:BaseUrl"];
            var flightFlowBaseUrl = configuration["FlightFlow:BaseUrl"];
            var pricingBaseUrl = configuration["Pricing:BaseUrl"];

            services.AddSingleton<IAirPriceOfferNormalizer, AirPriceOfferNormalizer>();

            services.AddHttpClient<IOfferProvider, OfferProvider>(client =>
            {
                if (!string.IsNullOrWhiteSpace(offerBaseUrl))
                    client.BaseAddress = new Uri(offerBaseUrl);
            });

            services.AddHttpClient<IFlightFlowProvider, FlightFlowProvider>(client =>
            {
                if (!string.IsNullOrWhiteSpace(flightFlowBaseUrl))
                    client.BaseAddress = new Uri(flightFlowBaseUrl);
            });

            services.AddHttpClient<IPricingProvider, PricingProvider>(client =>
            {
                if (!string.IsNullOrWhiteSpace(pricingBaseUrl))
                    client.BaseAddress = new Uri(pricingBaseUrl);
            });

            services.Configure<MockPaymentOptions>(configuration.GetSection("Payment:Mock"));
            services.AddSingleton<MockPaymentStore>();
            services.AddScoped<IPaymentProvider, MockPaymentProvider>();

            if (configuration.GetValue<bool>(DeterministicAdapterOptions.EnabledKey))
            {
                services.AddSingleton<DeterministicReservationAdapter>();
                services.AddSingleton<DeterministicFundingCoverageAdapter>();
                services.AddSingleton<DeterministicDocumentIssuanceAdapter>();
                services.AddScoped<IReservationPort>(provider => provider.GetRequiredService<DeterministicReservationAdapter>());
                services.AddScoped<IFundingCoveragePort>(provider => provider.GetRequiredService<DeterministicFundingCoverageAdapter>());
                services.AddScoped<IDocumentIssuancePort>(provider => provider.GetRequiredService<DeterministicDocumentIssuanceAdapter>());
                services.AddSingleton<DeterministicEmdIssuanceAdapter>();
                services.AddScoped<IEmdIssuancePort>(provider => provider.GetRequiredService<DeterministicEmdIssuanceAdapter>());
                services.AddSingleton<DeterministicOrderChangeQuoteAdapter>();
                services.AddScoped<IOrderChangeQuoteProvider>(provider => provider.GetRequiredService<DeterministicOrderChangeQuoteAdapter>());
                services.AddSingleton<DeterministicOrderCancellationQuoteAdapter>();
                services.AddScoped<IOrderCancellationQuoteProvider>(provider => provider.GetRequiredService<DeterministicOrderCancellationQuoteAdapter>());
            }
            else
            {
                services.AddScoped<IOrderChangeQuoteProvider, UnconfiguredOrderChangeQuoteProvider>();
                services.AddScoped<IOrderCancellationQuoteProvider, UnconfiguredOrderCancellationQuoteProvider>();
                services.AddScoped<IEmdIssuancePort, UnconfiguredEmdIssuanceProvider>();
            }

            return services;
        }
    }
}
