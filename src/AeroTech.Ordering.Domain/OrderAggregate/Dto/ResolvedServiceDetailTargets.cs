namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    internal sealed record ResolvedServiceDetailTargets(
        long? SegmentId = null,
        long? AirServiceId = null);
}
