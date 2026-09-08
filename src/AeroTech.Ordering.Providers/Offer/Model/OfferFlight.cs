namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferFlight(
        long FlightId,
        int FlightVersion,
        string Number,
        long OriginAirportId,
        long? OriginAirportTerminalId,
        long DestinationAirportId,
        long? DestinationAirportTerminalId,
        long OperatingAirlineId,
        long MarketingAirlineId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        int Duration,
        long? AircraftId,
        long? CabinClassId,
        long? RbdId,
        string? BookingClass,
        long FlightCapacityId,
        IReadOnlyList<OfferFlightLeg> Legs);
}
