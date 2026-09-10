using AeroTech.Messages.FlightFlow.Enums;

namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record FlightHeldSeatsResult(
        string HoldId,
        string IdempotencyKey,
        string Reference,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<HeldSeat> Seats);

    public sealed record HeldSeat(
        string FlightId,
        string PaxReference,
        string? Seat,
        FlightSeatHoldStatus? SeatHoldStatus,
        string SeatHoldReference);
}
