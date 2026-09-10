namespace AeroTech.Ordering.Domain.Providers.FlightFlow
{
    public sealed record CancelConfirmedSeatsResult(bool Cancelled, string? Reason);
}
