using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Funding
{
    public sealed record FundingCoverageResult(
        FundingCoverageOutcome Outcome,
        decimal ConfirmedAmount,
        int CurrencyId,
        string? ExternalApplicationRef = null,
        string? Detail = null);
}
