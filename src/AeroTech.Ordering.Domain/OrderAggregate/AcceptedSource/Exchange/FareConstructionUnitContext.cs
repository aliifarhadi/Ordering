using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record FareConstructionUnitContext(
        int Sequence,
        FarePricingUnitType? UnitType,
        FareCombinationMethod? CombinationMethod,
        string? SourceReference,
        IReadOnlyList<FareConstructionComponentContext> Components);
}
