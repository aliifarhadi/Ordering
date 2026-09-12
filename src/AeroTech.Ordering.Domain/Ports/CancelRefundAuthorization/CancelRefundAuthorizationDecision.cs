using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.CancelRefundAuthorization
{
    public sealed record CancelRefundAuthorizationDecision(
        ManualRefundAuthorizationOutcome Outcome,
        string? DecisionReference = null,
        string? Detail = null);
}
