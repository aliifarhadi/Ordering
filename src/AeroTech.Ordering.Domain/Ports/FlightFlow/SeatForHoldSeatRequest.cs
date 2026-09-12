namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record SeatForHoldSeatRequest(
        string PaxReference,
        decimal Revenue,
        string? Seat);
}
