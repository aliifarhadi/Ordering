using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderPriceChangeSetArgs(
        long Id,
        long OrderId,
        long ChangeId,
        long FinancialSequence,
        int ExpectedCommercialVersion,
        PriceChangeReason Reason,
        PricingSource Source,
        DateTimeOffset CreatedAt,
        string? SourceOfferId = null,
        string? SourcePricingRef = null);
}
