using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.Providers.FlightFlow;
using AeroTech.Ordering.Domain.Providers.Payment;
using AeroTech.Ordering.Domain.Providers.Pricing;
using AeroTech.Ordering.Providers.FlightFlow.Services;
using AeroTech.Ordering.Providers.Offer.Services;
using AeroTech.Ordering.Providers.Payment.Services;
using AeroTech.Ordering.Providers.Payment.Options;
using AeroTech.Ordering.Providers.Pricing.Services;
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

            return services;
        }
    }
}
