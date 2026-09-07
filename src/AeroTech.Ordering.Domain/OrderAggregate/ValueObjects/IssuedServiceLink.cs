namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed record IssuedServiceLink(
        long OrderServiceId,
        long TrafficDocumentId,
        long DocumentCouponId);
}
