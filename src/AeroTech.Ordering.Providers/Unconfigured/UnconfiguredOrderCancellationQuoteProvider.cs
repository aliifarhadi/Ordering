using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredOrderCancellationQuoteProvider : IOrderCancellationQuoteProvider
    {
        public Task<AcceptedScopeCancellation> AcceptQuotedCancellationAsync(
            AcceptedQuotedCancellationSelection selection,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.OrderCancellationQuoteSourceNotConfigured();
    }
}
