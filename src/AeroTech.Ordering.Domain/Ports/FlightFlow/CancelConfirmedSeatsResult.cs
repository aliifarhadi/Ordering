namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record CancelConfirmedSeatsResult(bool Cancelled, string? Reason);
}
