using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.Ports.FlightFlow;
using AeroTech.Ordering.Domain.Ports.Payment;
using AeroTech.Ordering.Domain.Ports.Pricing;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Providers.FlightFlow.Services;
using AeroTech.Ordering.Providers.Offer.Services;
using AeroTech.Ordering.Providers.Payment.Options;
using AeroTech.Ordering.Providers.Payment.Services;
using AeroTech.Ordering.Providers.Pricing.Services;
using AeroTech.Ordering.Providers.Unconfigured;
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

            services.AddScoped<IOrderChangeQuoteProvider, UnconfiguredOrderChangeQuoteProvider>();
            services.AddScoped<IOrderCancellationQuoteProvider, UnconfiguredOrderCancellationQuoteProvider>();
            services.AddScoped<Domain.Ports.DocumentVoid.IDocumentVoidPort, UnconfiguredDocumentVoidProvider>();
            services.AddScoped<IEmdIssuancePort, UnconfiguredEmdIssuanceProvider>();
            services.AddScoped<Domain.Ports.Refund.IRefundQuotePort, UnconfiguredRefundQuoteProvider>();
            services.AddScoped<Domain.Ports.DocumentRefund.IDocumentRefundPort, UnconfiguredDocumentRefundProvider>();
            services.AddScoped<Domain.Ports.RefundValue.IRefundValuePort, UnconfiguredRefundValueProvider>();
            services.AddScoped<Domain.Ports.ManualRefundAuthorization.IManualRefundAuthorizationPort, UnconfiguredManualRefundAuthorizationProvider>();
            services.AddScoped<Domain.Ports.DocumentRefundCorrection.IDocumentRefundCorrectionPort, UnconfiguredDocumentRefundCorrectionProvider>();
            services.AddScoped<Domain.Ports.RefundValueCorrection.IRefundValueCorrectionPort, UnconfiguredRefundValueCorrectionProvider>();
            services.AddScoped<Domain.Ports.CancelRefundAuthorization.ICancelRefundAuthorizationPort, UnconfiguredCancelRefundAuthorizationProvider>();
            services.AddScoped<Domain.Ports.ChangeQuote.IChangeQuotePort, UnconfiguredChangeQuoteProvider>();
            services.AddScoped<Domain.Ports.ReservationChange.IReservationChangePort, UnconfiguredReservationChangeProvider>();
            services.AddScoped<Domain.Ports.DocumentChangeEligibility.IDocumentChangeEligibilityPort, UnconfiguredDocumentChangeEligibilityProvider>();
            services.AddScoped<Domain.Ports.DocumentRevalidation.IDocumentRevalidationPort, UnconfiguredDocumentRevalidationProvider>();
            services.AddScoped<Domain.Ports.Exchange.IExchangeQuotePort, UnconfiguredExchangeQuoteProvider>();
            services.AddScoped<Domain.Ports.DocumentExchange.IDocumentExchangePort, UnconfiguredDocumentExchangeProvider>();
            services.AddScoped<Domain.Ports.ExchangeFunding.IExchangeFundingPort, UnconfiguredExchangeFundingProvider>();

            return services;
        }
    }
}
