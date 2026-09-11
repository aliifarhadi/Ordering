using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicExchangeFundingAdapter : IExchangeFundingPort
    {
        private readonly Dictionary<string, DeterministicFundingOperation> _dispatched = new(StringComparer.Ordinal);

        public ProviderOperationOutcome GuaranteeOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome GuaranteeRecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public ProviderOperationOutcome CaptureOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome CaptureRecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public ProviderOperationOutcome ReleaseOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome ReleaseRecoveryOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public bool ThrowBeforeGuaranteeDispatch { get; set; }

        public bool ThrowAfterGuaranteeDispatch { get; set; }

        public bool ThrowBeforeCaptureDispatch { get; set; }

        public bool ThrowAfterCaptureDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public List<ExchangeFundingGuaranteeRequest> ObservedGuarantees { get; } = new();

        public List<ExchangeFundingCaptureRequest> ObservedCaptures { get; } = new();

        public List<ExchangeFundingReleaseRequest> ObservedReleases { get; } = new();

        public List<string> ObservedGuaranteeRecoveryKeys { get; } = new();

        public List<string> ObservedCaptureRecoveryKeys { get; } = new();

        public List<string> ObservedReleaseRecoveryKeys { get; } = new();

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched.Keys;

        public Task<ExchangeFundingResult> GuaranteeAsync(
            ExchangeFundingGuaranteeRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedGuarantees.Add(request);

            if (ThrowBeforeGuaranteeDispatch)
                throw new InvalidOperationException("The exchange funding guarantee never left Ordering.");

            var recorded = Remember(request.OperationKey, GuaranteeOutcome, request.Amount, request.CurrencyId, "GUAR");

            if (ThrowAfterGuaranteeDispatch)
                throw new InvalidOperationException("The exchange funding guarantee response never reached Ordering.");

            return Task.FromResult(recorded.AsResult());
        }

        public Task<ExchangeFundingRecovery> RecoverGuaranteeAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedGuaranteeRecoveryKeys.Add(request.OperationKey);

            return Recover(request.OperationKey, GuaranteeRecoveryOutcome);
        }

        public Task<ExchangeFundingResult> CaptureAsync(
            ExchangeFundingCaptureRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedCaptures.Add(request);

            if (ThrowBeforeCaptureDispatch)
                throw new InvalidOperationException("The exchange funding capture never left Ordering.");

            var recorded = Remember(request.OperationKey, CaptureOutcome, request.Amount, request.CurrencyId, "CAP");

            if (ThrowAfterCaptureDispatch)
                throw new InvalidOperationException("The exchange funding capture response never reached Ordering.");

            return Task.FromResult(recorded.AsResult());
        }

        public Task<ExchangeFundingRecovery> RecoverCaptureAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedCaptureRecoveryKeys.Add(request.OperationKey);

            return Recover(request.OperationKey, CaptureRecoveryOutcome);
        }

        public Task<ExchangeFundingResult> ReleaseAsync(
            ExchangeFundingReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedReleases.Add(request);

            var recorded = Remember(request.OperationKey, ReleaseOutcome, null, null, "REL");

            return Task.FromResult(recorded.AsResult());
        }

        public Task<ExchangeFundingRecovery> RecoverReleaseAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedReleaseRecoveryKeys.Add(request.OperationKey);

            return Recover(request.OperationKey, ReleaseRecoveryOutcome);
        }

        private DeterministicFundingOperation Remember(
            string operationKey,
            ProviderOperationOutcome outcome,
            decimal? amount,
            int? currencyId,
            string prefix)
        {
            if (_dispatched.TryGetValue(operationKey, out var existing))
                return existing;

            var recorded = new DeterministicFundingOperation(
                outcome,
                outcome == ProviderOperationOutcome.Rejected ? null : $"{prefix}-{Reference(operationKey)}",
                amount,
                currencyId);

            _dispatched[operationKey] = recorded;

            return recorded;
        }

        private Task<ExchangeFundingRecovery> Recover(string operationKey, ProviderOperationOutcome recoveryOutcome)
        {
            if (ThrowOnRecover)
                throw new InvalidOperationException("The exchange funding provider is unreachable.");

            if (!_dispatched.TryGetValue(operationKey, out var dispatched))
                return Task.FromResult(new ExchangeFundingRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such exchange funding operation"));

            var resolved = dispatched.Outcome == ProviderOperationOutcome.Confirmed
                           || dispatched.Outcome == ProviderOperationOutcome.Rejected
                ? dispatched.Outcome
                : recoveryOutcome;

            return Task.FromResult(new ExchangeFundingRecovery(
                true,
                resolved,
                resolved == ProviderOperationOutcome.Rejected ? null : dispatched.ProviderReference,
                dispatched.Amount,
                dispatched.CurrencyId));
        }

        private static string Reference(string operationKey) => operationKey.Replace(':', '-');
    }
}
