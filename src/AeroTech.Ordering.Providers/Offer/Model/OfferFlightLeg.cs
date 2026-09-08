namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferFlightLeg(
        long LegId,
        int Sequence,
        long OriginAirportId,
        long? OriginAirportTerminalId,
        long DestinationAirportId,
        long? DestinationAirportTerminalId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        OfferFlightStop? Stop);
}
