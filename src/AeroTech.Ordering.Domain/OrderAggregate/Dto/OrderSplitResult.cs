namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record OrderSplitResult(
        Order NewOrder,
        IReadOnlyDictionary<long, long> ServiceMap,
        IReadOnlyDictionary<long, long> TravellerMap);
}
