using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

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
        long PredecessorOrderServiceId,
        long PredecessorTicketCouponId,
        IReadOnlyList<long> ContinuedOrderServiceIds,
        AcceptedChangeReplacement Replacement,
        ChangeMonetaryOutcome MonetaryOutcome,
        IReadOnlyList<AcceptedExchangePricingLine> PricingLines,
        AcceptedSuccessorCoupon SuccessorCoupon,
        DateTimeOffset ExpiresAt,
        string? SourcePricingReference = null);
}
