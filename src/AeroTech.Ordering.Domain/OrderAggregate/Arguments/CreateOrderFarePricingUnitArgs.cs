using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderFarePricingUnitArgs(
        long Id,
        int Sequence,
        FarePricingUnitType? PricingUnitType = null,
        FareCombinationMethod? CombinationMethod = null,
        string? SourceReference = null);
}
