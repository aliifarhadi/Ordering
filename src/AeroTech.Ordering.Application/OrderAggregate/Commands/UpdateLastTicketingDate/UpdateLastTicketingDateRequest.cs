namespace AeroTech.Ordering.Application.OrderAggregate.Commands.UpdateLastTicketingDate
{
    public sealed record UpdateLastTicketingDateRequest(DateTimeOffset LastTicketingDate);
}
