namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments
{
    public sealed record ExchangeCouponScope(
        long TicketCouponId,
        long OrderServiceId);
}
