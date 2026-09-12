using AeroTech.Messages.FlightFlow.Enums;

namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record HeldSeat(
        string FlightId,
        string PaxReference,
        string? Seat,
        FlightSeatHoldStatus? SeatHoldStatus,
        string SeatHoldReference);
}
