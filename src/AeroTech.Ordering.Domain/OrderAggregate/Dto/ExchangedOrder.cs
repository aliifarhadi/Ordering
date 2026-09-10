namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record ExchangedOrder(
        long OrderChangeId,
        long PriceChangeSetId,
        long ReplacedOrderServiceId,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        long FinancialSequence,
        IReadOnlyDictionary<string, long> PricingLineIdsBySourceRef);
}
