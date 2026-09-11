using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record FareConstructionContext(
        string SourceSystem,
        string? SourcePricingReference,
        AirFareConstructionType? ConstructionType,
        IReadOnlyList<FareConstructionGroupContext> Groups);
}
