using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Providers.Offer.Model;

namespace AeroTech.Ordering.Providers.Offer.Services
{
    public interface IAirPriceOfferNormalizer
    {
        AcceptedOrderSource Normalize(OfferDetail offer);
    }
}
