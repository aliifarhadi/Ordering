namespace AeroTech.Ordering.Domain.Providers.FlightFlow
{
    public sealed record ReleaseHeldSeatsResult(bool Released, string? Reason);
}
