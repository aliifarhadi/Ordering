using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.OrderChange
{
    public sealed record AcceptedQuotedCancellationSelection(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedCancellationId,
        OrderChangeType Intent,
        int ExpectedCommercialVersion,
        long? OrderItemId,
        IReadOnlyList<long> OrderServiceIds,
        int SaleCurrencyId);
}
