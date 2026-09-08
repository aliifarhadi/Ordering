namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderFareComponentArgs(
        long Id,
        int Sequence,
        IReadOnlyList<long> OrderServiceIds,
        IReadOnlyList<long> OrderSegmentIds,
        DateTimeOffset CreatedAt,
        int? OriginAirportId = null,
        int? DestinationAirportId = null,
        string? FareBasis = null,
        string? BrandCode = null,
        string? BrandName = null,
        string? FareType = null,
        int? CabinClassId = null,
        long? RbdId = null,
        string? BookingClass = null,
        int? FareOwnerCarrierId = null,
        string? TariffReference = null,
        string? RuleReference = null,
        string? RoutingReference = null,
        string? SourceFareReference = null,
        string? SourceComponentReference = null);
}
