using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ManualRefundAuthorization
{
    public sealed record ManualRefundAuthorizationDecision(
        ManualRefundAuthorizationOutcome Outcome,
        string? DecisionReference = null,
        string? Detail = null);
}
