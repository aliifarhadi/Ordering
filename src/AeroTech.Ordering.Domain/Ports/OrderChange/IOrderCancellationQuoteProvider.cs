using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;

namespace AeroTech.Ordering.Domain.Ports.OrderChange
{
    public interface IOrderCancellationQuoteProvider
    {
        Task<AcceptedScopeCancellation> AcceptQuotedCancellationAsync(
            AcceptedQuotedCancellationSelection selection,
            CancellationToken cancellationToken = default);
    }

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
