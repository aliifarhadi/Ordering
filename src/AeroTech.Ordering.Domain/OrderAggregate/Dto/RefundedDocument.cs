namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record RefundedDocument(
        long OrderChangeId,
        long PriceChangeSetId,
        IReadOnlyList<long> RefundedServiceIds,
        long FinancialSequence);
}
