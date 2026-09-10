namespace AeroTech.Ordering.Domain.Ports.FlightFlow
{
    public sealed record ReleaseHeldSeatsResult(bool Released, string? Reason);
}
