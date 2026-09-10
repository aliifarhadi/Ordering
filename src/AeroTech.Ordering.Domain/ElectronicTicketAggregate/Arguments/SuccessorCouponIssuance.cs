using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments
{
    public sealed record SuccessorCouponIssuance(
        long CouponId,
        int CouponNumber,
        long PredecessorTicketCouponId,
        long OrderServiceId,
        long JourneySegmentId,
        IssuedSegmentSnapshot IssuedSegment,
        string? FareBasis,
        decimal IssuanceValue,
        IReadOnlyList<TicketCouponPriceLink> PriceLinks);
}
