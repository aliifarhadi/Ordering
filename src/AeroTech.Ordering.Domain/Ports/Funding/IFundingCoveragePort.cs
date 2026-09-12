namespace AeroTech.Ordering.Domain.Ports.Funding
{
    public interface IFundingCoveragePort
    {
        Task<FundingCoverageResult> VerifyCoverageAsync(FundingCoverageRequest request, CancellationToken cancellationToken = default);

        Task<FundingReleaseResult> RequestReleaseAsync(FundingReleaseRequest request, CancellationToken cancellationToken = default);
    }
}
