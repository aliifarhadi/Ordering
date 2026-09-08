using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderPricingAllocationSetArgs(
        long Id,
        long OrderIdAtCreation,
        PricingAllocationPurpose Purpose,
        int Version,
        PricingSource Source,
        PricingAllocationMethod Method,
        PricingAllocationCompleteness Completeness,
        DateTimeOffset CreatedAt,
        long? SupersedesAllocationSetId = null,
        string? PricingContextRef = null,
        string? PolicyVersion = null);
}
