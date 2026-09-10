using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeQuoteOutcome(
        long OrderId,
        int CommercialVersion,
        long PredecessorElectronicTicketId,
        string PredecessorDocumentNumber,
        long PredecessorOrderServiceId,
        long PredecessorTicketCouponId,
        string QuotedExchangeId,
        string SourceSystem,
        string TargetSelectionRef,
        PricingSource PricingSource,
        ChangeMonetaryOutcome MonetaryOutcome,
        int SaleCurrencyId,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<long> ContinuedOrderServiceIds,
        AcceptedChangeReplacement Replacement,
        IReadOnlyList<AcceptedExchangePricingLine> PricingLines,
        AcceptedSuccessorCoupon SuccessorCoupon,
        string? SourcePricingReference);
}
