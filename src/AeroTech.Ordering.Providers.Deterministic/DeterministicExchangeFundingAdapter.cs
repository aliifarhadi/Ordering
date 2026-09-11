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

        public bool ThrowBeforeReleaseDispatch { get; set; }

        public bool ThrowAfterReleaseDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public decimal? GuaranteeAmountOverride { get; set; }

        public int? GuaranteeCurrencyOverride { get; set; }

        public bool OmitGuaranteeReference { get; set; }

        public decimal? CaptureAmountOverride { get; set; }

        public int? CaptureCurrencyOverride { get; set; }

        public bool OmitCaptureReference { get; set; }

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

            var recorded = Remember(
                request.OperationKey,
                GuaranteeIntent(request),
                GuaranteeOutcome,
                OmitGuaranteeReference ? null : $"GUAR-{Reference(request.OperationKey)}",
                GuaranteeAmountOverride ?? request.Amount,
                GuaranteeCurrencyOverride ?? request.CurrencyId);

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

            var recorded = Remember(
                request.OperationKey,
                CaptureIntent(request),
                CaptureOutcome,
                OmitCaptureReference ? null : $"CAP-{Reference(request.OperationKey)}",
                CaptureAmountOverride ?? request.Amount,
                CaptureCurrencyOverride ?? request.CurrencyId);

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

            if (ThrowBeforeReleaseDispatch)
                throw new InvalidOperationException("The exchange funding release never left Ordering.");

            var recorded = Remember(
                request.OperationKey,
                ReleaseIntent(request),
                ReleaseOutcome,
                $"REL-{Reference(request.OperationKey)}",
                null,
                null);

            if (ThrowAfterReleaseDispatch)
                throw new InvalidOperationException("The exchange funding release response never reached Ordering.");

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
            string intent,
            ProviderOperationOutcome outcome,
            string? providerReference,
            decimal? amount,
            int? currencyId)
        {
            if (_dispatched.TryGetValue(operationKey, out var existing))
                return string.Equals(existing.Intent, intent, StringComparison.Ordinal)
                    ? existing
                    : throw new InvalidOperationException(
                        $"A different funding intent already owns operation key {operationKey}.");

            var recorded = new DeterministicFundingOperation(
                intent,
                outcome,
                outcome == ProviderOperationOutcome.Rejected ? null : providerReference,
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

            var resolved = dispatched.Resolved(recoveryOutcome);

            _dispatched[operationKey] = resolved;

            return Task.FromResult(new ExchangeFundingRecovery(
                true,
                resolved.Outcome,
                resolved.ProviderReference,
                resolved.Amount,
                resolved.CurrencyId));
        }

        private static string GuaranteeIntent(ExchangeFundingGuaranteeRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.QuotedExchangeId,
                request.PredecessorDocumentNumber,
                request.PayerTravellerId,
                request.Amount,
                request.CurrencyId,
                request.FundingMethodRef);

        private static string CaptureIntent(ExchangeFundingCaptureRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.QuotedExchangeId,
                request.SuccessorDocumentNumber,
                request.GuaranteeReference,
                request.Amount,
                request.CurrencyId);

        private static string ReleaseIntent(ExchangeFundingReleaseRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.QuotedExchangeId,
                request.GuaranteeReference,
                request.Reason);

        private static string Reference(string operationKey) => operationKey.Replace(':', '-');
    }
}
