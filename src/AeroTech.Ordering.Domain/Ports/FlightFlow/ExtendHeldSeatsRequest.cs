namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record ExtendHeldSeatsRequest(
        string HoldBatchId,
        DateTimeOffset ExpiresAt);
}
