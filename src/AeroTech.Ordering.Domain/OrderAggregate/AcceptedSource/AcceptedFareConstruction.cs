using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedFareConstruction(
        string ConstructionRef,
        string SourceSystem,
        IReadOnlyList<string> ProductRefs,
        IReadOnlyList<AcceptedFarePricingGroup> PricingGroups,
        AirFareConstructionType? ConstructionType = null,
        string? SourcePricingReference = null);
}
