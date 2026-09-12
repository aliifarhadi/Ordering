using AeroTech.Messages.FlightFlow.Enums;

namespace AeroTech.Ordering.Domain.Ports.Pricing
{
    public sealed record AirFareBoundReservationFlight(
        string FlightId,
        string FlightNumber,
        int FlightOriginAirportId,
        int FlightDestinationAirportId,
        int AircraftId,
        DateTimeOffset DepartureDateTime,
        FlightStatus FlightStatus,
        DateTimeOffset? FlightStopBookDateTime,
        long RbdId,
        int RequiredSeats);
}
