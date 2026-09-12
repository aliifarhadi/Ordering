namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record HoldSeatsRequest(
        string IdempotencyKey,
        string Reference,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<PassengerForHoldSeatRequest> Passengers,
        IReadOnlyList<FlightForHoldSeatRequest> Flights);
}
