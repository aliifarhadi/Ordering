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
                services.AddSingleton<DeterministicDocumentVoidAdapter>();
                services.AddScoped<Domain.Ports.DocumentVoid.IDocumentVoidPort>(provider => provider.GetRequiredService<DeterministicDocumentVoidAdapter>());
                services.AddSingleton<DeterministicRefundQuoteAdapter>();
                services.AddScoped<Domain.Ports.Refund.IRefundQuotePort>(provider => provider.GetRequiredService<DeterministicRefundQuoteAdapter>());
                services.AddSingleton<DeterministicDocumentRefundAdapter>();
                services.AddScoped<Domain.Ports.DocumentRefund.IDocumentRefundPort>(provider => provider.GetRequiredService<DeterministicDocumentRefundAdapter>());
                services.AddSingleton<DeterministicRefundValueAdapter>();
                services.AddScoped<Domain.Ports.RefundValue.IRefundValuePort>(provider => provider.GetRequiredService<DeterministicRefundValueAdapter>());
                services.AddSingleton<DeterministicDocumentRefundCorrectionAdapter>();
                services.AddScoped<Domain.Ports.DocumentRefundCorrection.IDocumentRefundCorrectionPort>(
                    provider => provider.GetRequiredService<DeterministicDocumentRefundCorrectionAdapter>());
                services.AddSingleton<DeterministicRefundValueCorrectionAdapter>();
                services.AddScoped<Domain.Ports.RefundValueCorrection.IRefundValueCorrectionPort>(
                    provider => provider.GetRequiredService<DeterministicRefundValueCorrectionAdapter>());
                services.AddSingleton<DeterministicCancelRefundAuthorizationAdapter>();
                services.AddScoped<Domain.Ports.CancelRefundAuthorization.ICancelRefundAuthorizationPort>(
                    provider => provider.GetRequiredService<DeterministicCancelRefundAuthorizationAdapter>());
                services.AddSingleton<DeterministicManualRefundAuthorizationAdapter>();
                services.AddScoped<Domain.Ports.ManualRefundAuthorization.IManualRefundAuthorizationPort>(
                    provider => provider.GetRequiredService<DeterministicManualRefundAuthorizationAdapter>());
            }
            else
            {
                services.AddScoped<IOrderChangeQuoteProvider, UnconfiguredOrderChangeQuoteProvider>();
                services.AddScoped<IOrderCancellationQuoteProvider, UnconfiguredOrderCancellationQuoteProvider>();
                services.AddScoped<Domain.Ports.DocumentVoid.IDocumentVoidPort, DocumentVoid.Services.UnconfiguredDocumentVoidProvider>();
                services.AddScoped<IEmdIssuancePort, UnconfiguredEmdIssuanceProvider>();
                services.AddScoped<Domain.Ports.Refund.IRefundQuotePort, Refund.Services.UnconfiguredRefundQuoteProvider>();
                services.AddScoped<Domain.Ports.DocumentRefund.IDocumentRefundPort, DocumentRefund.Services.UnconfiguredDocumentRefundProvider>();
                services.AddScoped<Domain.Ports.RefundValue.IRefundValuePort, RefundValue.Services.UnconfiguredRefundValueProvider>();
                services.AddScoped<Domain.Ports.ManualRefundAuthorization.IManualRefundAuthorizationPort,
                    ManualRefundAuthorization.Services.UnconfiguredManualRefundAuthorizationProvider>();
                services.AddScoped<Domain.Ports.DocumentRefundCorrection.IDocumentRefundCorrectionPort,
                    DocumentRefundCorrection.Services.UnconfiguredDocumentRefundCorrectionProvider>();
                services.AddScoped<Domain.Ports.RefundValueCorrection.IRefundValueCorrectionPort,
                    RefundValueCorrection.Services.UnconfiguredRefundValueCorrectionProvider>();
                services.AddScoped<Domain.Ports.CancelRefundAuthorization.ICancelRefundAuthorizationPort,
                    CancelRefundAuthorization.Services.UnconfiguredCancelRefundAuthorizationProvider>();
            }

            return services;
        }
    }
}
