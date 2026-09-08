namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record AddedProduct(
        long OrderChangeId,
        long OrderItemId,
        IReadOnlyList<long> OrderServiceIds,
        long PriceChangeSetId,
        long FinancialSequence);
}
