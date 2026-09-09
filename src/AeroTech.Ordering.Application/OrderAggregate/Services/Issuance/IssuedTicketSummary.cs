namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record IssuedTicketSummary(long TicketId, long TravelerId, string DocumentNumber, int CouponCount);
}
