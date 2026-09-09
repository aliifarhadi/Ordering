namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record CorrectedRefund(
        long OrderChangeId,
        long PriceChangeSetId,
        long RefundRecordId,
        IReadOnlyList<long> RestoredOrderServiceIds,
        long FinancialSequence);
}
