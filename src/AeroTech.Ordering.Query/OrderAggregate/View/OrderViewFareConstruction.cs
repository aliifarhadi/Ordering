using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record OrderViewFareConstruction(
        long FareConstructionId,
        long CreatedByChangeId,
        long? SupersedesConstructionId,
        AirFareConstructionType? ConstructionType,
        string SourceSystem,
        string? SourcePricingReference,
        DateTimeOffset CreatedAt,
        IReadOnlyList<long> ItemMembership,
        IReadOnlyList<OrderViewFarePricingGroup> PricingGroups);

    public sealed record OrderViewFarePricingGroup(
        long PricingGroupId,
        PassengerTypeCode? PassengerType,
        string? SourceReference,
        IReadOnlyList<long> TravellerIds,
        IReadOnlyList<OrderViewFarePricingUnit> PricingUnits);

    public sealed record OrderViewFarePricingUnit(
        long PricingUnitId,
        FarePricingUnitType? PricingUnitType,
        FareCombinationMethod? CombinationMethod,
        int Sequence,
        string? SourceReference,
        IReadOnlyList<OrderViewFareComponent> FareComponents);

    public sealed record OrderViewFareComponent(
        long FareComponentId,
        int Sequence,
        int? OriginAirportId,
        int? DestinationAirportId,
        string? FareBasis,
        string? BrandCode,
        string? BrandName,
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
        IReadOnlyList<long> ServiceIds,
        IReadOnlyList<long> SegmentIds);
}
