namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAddedLoungeDetail(
        int AirportId,
        int GuestCount,
        string? LoungeCode = null,
        DateTimeOffset? AccessStart = null,
        DateTimeOffset? AccessEnd = null,
        long? RelatedAirOrderServiceId = null) : AcceptedServiceDetail;
}
