using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketIssuedCoupon(
        long TicketCouponId,
        int CouponNumber,
        long OrderServiceId,
        long JourneySegmentId,
        decimal IssuanceValue,
        long? PredecessorTicketCouponId);
}
