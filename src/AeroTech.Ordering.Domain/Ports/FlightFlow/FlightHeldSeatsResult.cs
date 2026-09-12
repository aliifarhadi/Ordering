namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record FlightHeldSeatsResult(
        string HoldId,
        string IdempotencyKey,
        string Reference,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<HeldSeat> Seats);
}
