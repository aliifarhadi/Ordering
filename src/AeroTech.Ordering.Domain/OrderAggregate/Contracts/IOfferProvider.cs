using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;

namespace AeroTech.Ordering.Domain.OrderAggregate.Contracts
{
    public interface IOfferProvider
    {
        Task<AcceptedOrderSource> GetAcceptedSourceAsync(string offerId, CancellationToken cancellationToken = default);
    }
}
