namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    internal sealed record StagedRefund(StagedPriceChange PriceChange, IReadOnlyList<long> ServiceIds);
}
