namespace AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange
{
    public sealed record OrderChangeOutcome(
        long OrderId,
        long OperationId,
        long OrderChangeId,
        long PriceChangeSetId,
        long OrderItemId,
        IReadOnlyList<long> ServiceIds,
        long FinancialSequence,
        int CommercialVersion,
        long ObligationVersion,
        decimal CustomerTotal,
        int CurrencyId,
        bool IsReplay);
}
