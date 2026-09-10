namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record ExchangedOrder(
        long OrderChangeId,
        long PriceChangeSetId,
        IReadOnlyList<ExchangedServiceBinding> Coupons,
        long FinancialSequence,
        IReadOnlyDictionary<string, long> PricingLineIdsBySourceRef);
}
