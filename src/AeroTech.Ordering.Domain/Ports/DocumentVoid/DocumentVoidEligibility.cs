using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentVoid
{
    public sealed record DocumentVoidEligibility(
        EligibilityOutcome Outcome,
        bool RefundRequiredInstead = false,
        string? Detail = null);
}
