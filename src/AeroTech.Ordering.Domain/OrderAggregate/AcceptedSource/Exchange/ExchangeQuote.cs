using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record ExchangeQuote(
        string SourceSystem,
        string QuotedExchangeId,
        string TargetSelectionRef,
        PricingSource PricingSource,
        long OrderId,
        int ExpectedCommercialVersion,
        int SaleCurrencyId,
        long PredecessorElectronicTicketId,
        IReadOnlyList<long> ChangedOrderServiceIds,
        IReadOnlyList<AcceptedExchangeCoupon> Coupons,
        ChangeMonetaryOutcome MonetaryOutcome,
        IReadOnlyList<AcceptedExchangePricingLine> PricingLines,
        DateTimeOffset ExpiresAt,
        string? SourcePricingReference = null);
}
