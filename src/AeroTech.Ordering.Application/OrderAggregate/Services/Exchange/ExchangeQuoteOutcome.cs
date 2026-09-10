using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeQuoteOutcome(
        long OrderId,
        int CommercialVersion,
        long PredecessorElectronicTicketId,
        string PredecessorDocumentNumber,
        IReadOnlyList<long> ChangedOrderServiceIds,
        string QuotedExchangeId,
        string SourceSystem,
        string TargetSelectionRef,
        PricingSource PricingSource,
        ChangeMonetaryOutcome MonetaryOutcome,
        int SaleCurrencyId,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<AcceptedExchangeCoupon> Coupons,
        IReadOnlyList<AcceptedExchangePricingLine> PricingLines,
        string? SourcePricingReference);
}
