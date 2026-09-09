using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange
{
    public sealed record ChangeQuote(
        string SourceSystem,
        string QuotedChangeId,
        string TargetSelectionRef,
        PricingSource PricingSource,
        long OrderId,
        int ExpectedCommercialVersion,
        int SaleCurrencyId,
        long ElectronicTicketId,
        long ReplacedOrderServiceId,
        long ReplacedTicketCouponId,
        IReadOnlyList<long> ContinuedOrderServiceIds,
        AcceptedChangeReplacement Replacement,
        ChangeMonetaryOutcome MonetaryOutcome,
        DateTimeOffset ExpiresAt,
        string? SourcePricingReference = null);
}
