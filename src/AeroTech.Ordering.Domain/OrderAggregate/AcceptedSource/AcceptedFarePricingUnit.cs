using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedFarePricingUnit(
        string PricingUnitRef,
        int Sequence,
        IReadOnlyList<AcceptedFareComponent> FareComponents,
        FarePricingUnitType? PricingUnitType = null,
        FareCombinationMethod? CombinationMethod = null,
        string? SourceReference = null);
}
