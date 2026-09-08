namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferBound(
        string BoundId,
        int Sequence,
        long OriginAirportId,
        long DestinationAirportId,
        IReadOnlyList<OfferFlight> Flights);
}
