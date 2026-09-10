using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeScope(
        OrderService PredecessorService,
        ElectronicTicket PredecessorTicket,
        TicketCoupon PredecessorCoupon);
}
