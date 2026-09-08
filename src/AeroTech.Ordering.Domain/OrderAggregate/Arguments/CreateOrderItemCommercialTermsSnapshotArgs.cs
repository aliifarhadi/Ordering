using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderItemCommercialTermsSnapshotArgs(
        CommercialTermState RefundabilitySummary,
        CommercialTermState ChangeabilitySummary,
        CommercialTermState UpgradeEligibilitySummary,
        string SourceSystem,
        DateTimeOffset TermsCapturedAt,
        string? SourcePolicyReference = null,
        string? SourcePolicyVersion = null);
}
