using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;

namespace AeroTech.Ordering.Domain.Ports.OrderChange
{
    public interface IOrderChangeQuoteProvider
    {
        Task<AcceptedAddServiceChange> AcceptSelectedQuotedOfferAsync(
            AcceptedQuotedOfferSelection selection,
            CancellationToken cancellationToken = default);
    }
}
