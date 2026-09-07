namespace AeroTech.Ordering.Domain.Providers.FlightFlow
{
    public sealed record ExtendHeldSeatsRequest(
        string HoldBatchId,
        DateTimeOffset ExpiresAt);
}
