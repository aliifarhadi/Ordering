namespace AeroTech.Ordering.Domain.Providers.FlightFlow
{
    public sealed record SplitHeldSeatsResult(
        string NewHoldBatchId,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<string> SeatHoldReferences);
}
