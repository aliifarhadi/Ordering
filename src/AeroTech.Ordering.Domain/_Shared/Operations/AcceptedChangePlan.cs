using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

namespace AeroTech.Ordering.Domain._Shared.Operations
{
    public sealed record AcceptedChangePlan(
        long OperationId,
        long OrderId,
        string QuotedChangeId,
        string SourceSystem,
        string TargetSelectionRef,
        long ElectronicTicketId,
        long ReplacedOrderServiceId,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        long TicketCouponId,
        int ExpectedCommercialVersion,
        ChangeMonetaryOutcome MonetaryOutcome,
        AcceptedVoluntaryChange Accepted,
        ProviderOperationOutcome ReservationOutcome = ProviderOperationOutcome.Pending,
        string? ReservationExternalRef = null);
}
