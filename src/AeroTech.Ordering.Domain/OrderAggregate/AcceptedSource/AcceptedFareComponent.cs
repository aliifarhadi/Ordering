namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedFareComponent(
        string FareComponentRef,
        int Sequence,
        IReadOnlyList<string> ServiceRefs,
        IReadOnlyList<string> SegmentRefs,
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
