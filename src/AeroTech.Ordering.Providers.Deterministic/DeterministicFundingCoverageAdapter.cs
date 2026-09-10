using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.Funding;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicFundingCoverageAdapter : IFundingCoveragePort
    {
        public FundingCoverageOutcome Outcome { get; set; } = FundingCoverageOutcome.Confirmed;

        public decimal? ConfirmedAmountOverride { get; set; }

        public ProviderOperationOutcome ReleaseOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public List<string> ObservedOperationKeys { get; } = new();

        public Task<FundingCoverageResult> VerifyCoverageAsync(FundingCoverageRequest request, CancellationToken cancellationToken = default)
        {
            ObservedOperationKeys.Add(request.OperationKey);

            var confirmed = Outcome == FundingCoverageOutcome.Confirmed
                ? ConfirmedAmountOverride ?? request.RequiredAmount
                : ConfirmedAmountOverride ?? decimal.Zero;

            return Task.FromResult(new FundingCoverageResult(
                Outcome,
                confirmed,
                request.CurrencyId,
                $"COV-{request.OperationId}"));
        }

        public Task<FundingReleaseResult> RequestReleaseAsync(FundingReleaseRequest request, CancellationToken cancellationToken = default)
        {
            ObservedOperationKeys.Add(request.OperationKey);
            return Task.FromResult(new FundingReleaseResult(ReleaseOutcome));
        }
    }
}
