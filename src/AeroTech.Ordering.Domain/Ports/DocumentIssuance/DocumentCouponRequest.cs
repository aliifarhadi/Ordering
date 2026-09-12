namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public sealed record DocumentCouponRequest(
        long OrderServiceId,
        long JourneySegmentId,
        decimal AttributedValue);
}
