namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record RestoredDocumentLink(
        long OrderServiceId,
        long ElectronicTicketId,
        long TicketCouponId);
}
