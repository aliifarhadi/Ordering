using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedCommercialTerms(
        CommercialTermState RefundabilitySummary,
        CommercialTermState ChangeabilitySummary,
        CommercialTermState UpgradeEligibilitySummary,
        string SourceSystem,
        string? SourcePolicyReference = null,
        string? SourcePolicyVersion = null);
}
