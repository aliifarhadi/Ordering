using AeroTech.Ordering.Domain.ElectronicTicketAggregate;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record MaterializedExchange(
        ElectronicTicket Successor,
        long OrderChangeId,
        long PriceChangeSetId);
}
