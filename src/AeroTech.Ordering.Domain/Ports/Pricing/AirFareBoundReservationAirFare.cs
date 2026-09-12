namespace AeroTech.Ordering.Domain.Ports.Pricing
{
    public sealed record AirFareBoundReservationAirFare(
        string AirFareId,
        IReadOnlyList<AirFareBoundReservationFlight> Flights);
}
