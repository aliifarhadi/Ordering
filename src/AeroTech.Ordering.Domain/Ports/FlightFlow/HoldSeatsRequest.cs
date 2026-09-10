using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record HoldSeatsRequest(
        string IdempotencyKey,
        string Reference,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<PassengerForHoldSeatRequest> Passengers,
        IReadOnlyList<FlightForHoldSeatRequest> Flights);

    public sealed record PassengerForHoldSeatRequest(
        string PaxReference,
        PassengerTypeCode Type,
        Gender Gender);

    public sealed record FlightForHoldSeatRequest(
        string FlightCapId,
        IReadOnlyList<SeatForHoldSeatRequest> Seats);

    public sealed record SeatForHoldSeatRequest(
        string PaxReference,
        decimal Revenue,
        string? Seat);
}
