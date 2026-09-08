namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedCommercialTerms(
        bool IsRefundable,
        bool IsChangeable,
        bool IsUpgradable,
        AcceptedBaggageAllowance? CheckedBaggage,
        AcceptedBaggageAllowance? CabinBaggage,
        string PolicySource,
        string? SourceRuleReference);
}
