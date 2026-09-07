using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Funding
{
    public interface IFundingCoveragePort
    {
        Task<FundingCoverageResult> VerifyCoverageAsync(FundingCoverageRequest request, CancellationToken cancellationToken = default);

        Task<FundingReleaseResult> RequestReleaseAsync(FundingReleaseRequest request, CancellationToken cancellationToken = default);
    }

    public sealed record FundingCoverageRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long ObligationVersion,
        decimal RequiredAmount,
        int CurrencyId);

    public sealed record FundingCoverageResult(
        FundingCoverageOutcome Outcome,
        decimal ConfirmedAmount,
        int CurrencyId,
        string? ExternalApplicationRef = null,
        string? Detail = null);

    public sealed record FundingReleaseRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string? ExternalApplicationRef);

    public sealed record FundingReleaseResult(ProviderOperationOutcome Outcome, string? Detail = null);
}
