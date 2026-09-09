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
        string? ReservationExternalRef = null,
        DocumentChangeEligibilityOutcome? EligibilityOutcome = null,
        string? EligibilityDetail = null,
        ProviderOperationOutcome? RevalidationOutcome = null,
        string? RevalidationProviderReference = null,
        string? RevalidationDetail = null)
    {
        public bool IsReservationConfirmed
            => ReservationOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsRevalidationConfirmed
            => RevalidationOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsRevalidationEstablished
            => EligibilityOutcome == DocumentChangeEligibilityOutcome.Revalidate;

        public bool IsEligibilityTerminal
            => EligibilityOutcome is DocumentChangeEligibilityOutcome.ReissueRequired
                or DocumentChangeEligibilityOutcome.Denied;
    }
}
