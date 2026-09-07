using AeroTech.Ordering.Domain.OrderAggregate.Offers;

namespace AeroTech.Ordering.Domain.OrderAggregate.Contracts
{
    public interface IOfferProvider
    {
        Task<OfferDetail> GetByOfferIdAsync(string offerId, CancellationToken cancellationToken = default);
    }
}
