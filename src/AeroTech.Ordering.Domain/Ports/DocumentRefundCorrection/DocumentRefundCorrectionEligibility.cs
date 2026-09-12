using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection
{
    public sealed record DocumentRefundCorrectionEligibility(EligibilityOutcome Outcome, string? Detail = null);
}
