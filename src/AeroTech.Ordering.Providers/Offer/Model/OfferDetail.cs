using AeroTech.Ordering.Providers.Offer.Model;

namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferDetail(
        string OfferId,
        int CurrencyId,
        DateTimeOffset? LastTicketingDate,
        IReadOnlyList<OfferTraveller> Travellers,
        IReadOnlyList<OfferBound> Bounds,
        IReadOnlyList<OfferFareComponent> FareComponents,
        IReadOnlyList<OfferPriceLine> PriceLines,
        IReadOnlyList<OfferPriceLine> OrderCharges,
        IReadOnlyList<OfferCharge> Charges,
        IReadOnlyList<OfferRate> Rates);
}
