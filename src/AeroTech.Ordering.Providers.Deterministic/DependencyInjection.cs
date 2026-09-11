using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain.Ports.Funding;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain.Ports.Reservation;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDeterministicProviders(this IServiceCollection services)
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
            services.AddSingleton<DeterministicChangeQuoteAdapter>();
            services.AddScoped<Domain.Ports.ChangeQuote.IChangeQuotePort>(
                provider => provider.GetRequiredService<DeterministicChangeQuoteAdapter>());
            services.AddSingleton<DeterministicReservationChangeAdapter>();
            services.AddScoped<Domain.Ports.ReservationChange.IReservationChangePort>(
                provider => provider.GetRequiredService<DeterministicReservationChangeAdapter>());
            services.AddSingleton<DeterministicExchangeFundingAdapter>();
            services.AddScoped<Domain.Ports.ExchangeFunding.IExchangeFundingPort>(
                provider => provider.GetRequiredService<DeterministicExchangeFundingAdapter>());
            services.AddSingleton<DeterministicExchangeResidualAdapter>();
            services.AddScoped<Domain.Ports.ExchangeResidual.IExchangeResidualValuePort>(
                provider => provider.GetRequiredService<DeterministicExchangeResidualAdapter>());
            services.AddSingleton<DeterministicDocumentChangeEligibilityAdapter>();
            services.AddScoped<Domain.Ports.DocumentChangeEligibility.IDocumentChangeEligibilityPort>(
                provider => provider.GetRequiredService<DeterministicDocumentChangeEligibilityAdapter>());
            services.AddSingleton<DeterministicDocumentRevalidationAdapter>();
            services.AddScoped<Domain.Ports.DocumentRevalidation.IDocumentRevalidationPort>(
                provider => provider.GetRequiredService<DeterministicDocumentRevalidationAdapter>());
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
            services.AddSingleton<DeterministicExchangeQuoteAdapter>();
            services.AddScoped<Domain.Ports.Exchange.IExchangeQuotePort>(
                provider => provider.GetRequiredService<DeterministicExchangeQuoteAdapter>());
            services.AddSingleton<DeterministicDocumentExchangeAdapter>();
            services.AddScoped<Domain.Ports.DocumentExchange.IDocumentExchangePort>(
                provider => provider.GetRequiredService<DeterministicDocumentExchangeAdapter>());
            services.AddSingleton<DeterministicAncillaryDispositionAdapter>();
            services.AddScoped<Domain.Ports.AncillaryDisposition.IAncillaryExchangeDispositionPort>(
                provider => provider.GetRequiredService<DeterministicAncillaryDispositionAdapter>());
            services.AddSingleton<DeterministicEmdAssociationAdapter>();
            services.AddScoped<Domain.Ports.EmdAssociation.IEmdAssociationPort>(
                provider => provider.GetRequiredService<DeterministicEmdAssociationAdapter>());

            return services;
        }
    }
}
