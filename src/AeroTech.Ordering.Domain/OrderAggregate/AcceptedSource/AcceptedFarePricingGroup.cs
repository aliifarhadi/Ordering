using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedFarePricingGroup(
        string PricingGroupRef,
        IReadOnlyList<string> TravellerRefs,
        IReadOnlyList<AcceptedFarePricingUnit> PricingUnits,
        PassengerTypeCode? PassengerTypeCode = null,
        string? SourceReference = null);
}
