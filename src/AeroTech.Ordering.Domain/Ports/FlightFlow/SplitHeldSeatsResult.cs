namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record SplitHeldSeatsResult(
        string NewHoldBatchId,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<string> SeatHoldReferences);
}
