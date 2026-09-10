using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.OrderChange.Services
{
    public sealed class UnconfiguredOrderChangeQuoteProvider : IOrderChangeQuoteProvider
    {
        public Task<AcceptedAddServiceChange> AcceptSelectedQuotedOfferAsync(
            AcceptedQuotedOfferSelection selection,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.OrderChangeQuoteSourceNotConfigured();
    }
}
