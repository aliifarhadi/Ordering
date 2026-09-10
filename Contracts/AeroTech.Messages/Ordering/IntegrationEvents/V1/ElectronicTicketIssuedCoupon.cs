using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketIssuedCoupon(
        long TicketCouponId,
        int CouponNumber,
        long OrderServiceId,
        long JourneySegmentId,
        decimal IssuanceValue,
        long? PredecessorTicketCouponId);
}
