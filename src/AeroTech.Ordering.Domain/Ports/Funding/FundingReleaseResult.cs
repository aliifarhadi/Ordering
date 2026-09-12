using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Funding
{
    public sealed record FundingReleaseResult(ProviderOperationOutcome Outcome, string? Detail = null);
}
