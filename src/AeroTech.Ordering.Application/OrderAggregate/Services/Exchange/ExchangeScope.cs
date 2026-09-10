using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeScope(
        ElectronicTicket PredecessorTicket,
        IReadOnlyList<long> ChangedOrderServiceIds,
        IReadOnlyList<PredecessorCouponEvidence> Coupons);
}
