using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedDependentPriceChangeArgs(
        long OperationId,
        OrderChangeType HostChangeType,
        PriceChangeReason Reason,
        PricingSource Source,
        IReadOnlyList<AcceptedPricingLineArgs> Lines,
        string? SourceOfferId = null,
        string? SourcePricingRef = null);
}
