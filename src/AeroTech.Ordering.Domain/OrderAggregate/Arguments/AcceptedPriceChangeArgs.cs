using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedPriceChangeArgs(
        OrderChangeType ChangeType,
        PriceChangeReason Reason,
        PricingSource Source,
        IReadOnlyList<AcceptedPricingLineArgs> Lines,
        string? SourceOfferId = null,
        string? SourcePricingRef = null,
        string? ChangeReason = null,
        string? ExternalReference = null,
        string? ActorScope = null,
        long? ActorId = null,
        long? OperationId = null);
}
