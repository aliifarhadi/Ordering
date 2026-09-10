using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketRevalidated(
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long RevalidationRecordId,
        long TicketCouponId,
        int CouponNumber,
        long PreviousOrderServiceId,
        long NewOrderServiceId,
        string QuotedChangeId,
        string TargetSelectionRef,
        int DocumentVersion) : BaseIntegrationEvent;
}
