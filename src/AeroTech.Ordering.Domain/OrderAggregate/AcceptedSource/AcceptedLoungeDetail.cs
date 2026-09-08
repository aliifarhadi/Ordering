namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedLoungeDetail(
        int AirportId,
        int GuestCount,
        string? LoungeCode = null,
        DateTimeOffset? AccessStart = null,
        DateTimeOffset? AccessEnd = null,
        string? RelatedAirServiceRef = null) : AcceptedServiceDetail;
}
