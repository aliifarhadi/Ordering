using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentRefund
{
    public sealed record DocumentRefundEligibility(EligibilityOutcome Outcome, string? Detail = null);
}
