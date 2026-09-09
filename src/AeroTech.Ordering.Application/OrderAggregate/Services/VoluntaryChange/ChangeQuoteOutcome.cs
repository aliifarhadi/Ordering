using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange
{
    public sealed record ChangeQuoteOutcome(
        long OrderId,
        int CommercialVersion,
        long ElectronicTicketId,
        string DocumentNumber,
        long OrderServiceId,
        long TicketCouponId,
        string QuotedChangeId,
        string SourceSystem,
        string TargetSelectionRef,
        ChangeMonetaryOutcome MonetaryOutcome,
        int SaleCurrencyId,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<long> ContinuedOrderServiceIds,
        AcceptedChangeReplacement Replacement,
        string? SourcePricingReference);
}
