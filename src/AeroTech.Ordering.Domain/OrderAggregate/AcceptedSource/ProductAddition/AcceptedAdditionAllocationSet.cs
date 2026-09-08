using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAdditionAllocationSet(
        PricingAllocationPurpose Purpose,
        PricingSource Source,
        PricingAllocationMethod Method,
        PricingAllocationCompleteness Completeness,
        IReadOnlyList<AcceptedAdditionAllocation> Allocations,
        string? PricingContextRef = null,
        string? PolicyVersion = null);
}
