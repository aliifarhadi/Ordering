namespace AeroTech.Ordering.Application.OrderAggregate.Commands.SplitOrder
{
    public sealed record SplitOrderRequest(
        IReadOnlyCollection<long> TravellerIds);
}
