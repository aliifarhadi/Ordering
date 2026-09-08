using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderItemCommercialTermsSnapshotArgs(
        bool IsRefundable,
        bool IsChangeable,
        bool IsUpgradable,
        string PolicySource,
        DateTimeOffset TermsCapturedAt,
        Baggage? CheckedBaggage = null,
        Baggage? CabinBaggage = null,
        string? SourceRuleReference = null);
}
