namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record FareConstructionComponentContext(
        int Sequence,
        int? OriginAirportId,
        int? DestinationAirportId,
        string? FareBasis,
        string? BrandCode,
        string? FareType,
        int? CabinClassId,
        long? RbdId,
        string? BookingClass,
        int? FareOwnerCarrierId,
        string? TariffReference,
        string? RuleReference,
        string? RoutingReference,
        string? SourceFareReference,
        string? SourceComponentReference,
        IReadOnlyList<long> OrderServiceIds,
        IReadOnlyList<long> OrderSegmentIds);
}
