using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;

namespace AeroTech.Ordering.Domain.Ports.OrderChange
{
    public interface IOrderCancellationQuoteProvider
    {
        Task<AcceptedScopeCancellation> AcceptQuotedCancellationAsync(
            AcceptedQuotedCancellationSelection selection,
            CancellationToken cancellationToken = default);
    }
}
