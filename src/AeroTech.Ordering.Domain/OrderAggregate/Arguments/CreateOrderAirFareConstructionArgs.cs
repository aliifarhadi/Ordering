using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderAirFareConstructionArgs(
        long Id,
        long OrderId,
        long CreatedByChangeId,
        string SourceSystem,
        DateTimeOffset CreatedAt,
        AirFareConstructionType? ConstructionType = null,
        string? SourcePricingReference = null,
        long? SupersedesConstructionId = null);
}
