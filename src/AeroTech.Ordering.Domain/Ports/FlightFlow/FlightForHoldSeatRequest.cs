namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record FlightForHoldSeatRequest(
        string FlightCapId,
        IReadOnlyList<SeatForHoldSeatRequest> Seats);
}
