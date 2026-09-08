namespace AeroTech.Ordering.Providers.Offer.Model
{
    internal sealed record PendingAirService(
        string ServiceRef,
        string TravellerRef,
        string SegmentRef);
}
