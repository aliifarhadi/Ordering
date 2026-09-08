using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedPricingAllocationSetArgs(
        PricingAllocationPurpose Purpose,
        PricingSource Source,
        PricingAllocationMethod Method,
        PricingAllocationCompleteness Completeness,
        IReadOnlyList<AcceptedPricingAllocationArgs> Allocations,
        string? PricingContextRef = null,
        string? PolicyVersion = null);
}
