using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.Providers.FlightFlow;
using AeroTech.Ordering.Domain.Providers.Payment;
using AeroTech.Ordering.Domain.Providers.Pricing;
using AeroTech.Ordering.Providers.FlightFlow.Services;
using AeroTech.Ordering.Providers.Offer.Services;
using AeroTech.Ordering.Providers.Payment.Services;
using AeroTech.Ordering.Providers.Payment.Options;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain.Ports.Funding;
using AeroTech.Ordering.Domain.Ports.ProductAddition;
using AeroTech.Ordering.Domain.Ports.Reservation;
using AeroTech.Ordering.Providers.ProductAddition.Services;
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
                services.AddSingleton<DeterministicProductAdditionAdapter>();
                services.AddScoped<IAcceptedProductAdditionPort>(provider => provider.GetRequiredService<DeterministicProductAdditionAdapter>());
            }
            else
            {
                services.AddScoped<IAcceptedProductAdditionPort, UnconfiguredProductAdditionProvider>();
            }

            return services;
        }
    }
}
