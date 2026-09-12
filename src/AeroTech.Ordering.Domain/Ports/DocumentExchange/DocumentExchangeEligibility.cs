using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record DocumentExchangeEligibility(
        DocumentExchangeEligibilityOutcome Outcome,
        string? Detail = null);
}
