namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record SplitNewOrderSnapshot(
        long OrderId,
        string? RecordLocator,
        Guid UniqueIdentifierId,
        int Version,
        decimal GrandTotal,
        IReadOnlyList<PricingLineSnapshot> PricingLines);
}
