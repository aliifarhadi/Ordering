namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record ChangedServices(
        long OrderChangeId,
        long ReplacedOrderServiceId,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        long ElectronicTicketId,
        long TicketCouponId);
}
