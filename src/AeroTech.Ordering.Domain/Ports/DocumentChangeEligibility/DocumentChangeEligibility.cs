using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility
{
    public sealed record DocumentChangeEligibility(
        DocumentChangeEligibilityOutcome Outcome,
        string? Detail = null);
}
