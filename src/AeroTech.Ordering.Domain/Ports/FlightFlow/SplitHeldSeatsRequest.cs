namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record SplitHeldSeatsRequest(
        string HoldBatchId,
        IReadOnlyList<string> SeatHoldReferences,
        string IdempotencyKey,
        string Reference,
        DateTimeOffset ExpiresAt);
}
