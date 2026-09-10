namespace AeroTech.Ordering.Domain.Providers.FlightFlow
{
    public sealed record SplitHeldSeatsRequest(
        string HoldBatchId,
        IReadOnlyList<string> SeatHoldReferences,
        string IdempotencyKey,
        string Reference,
        DateTimeOffset ExpiresAt);
}
