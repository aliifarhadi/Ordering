using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.RefundValue;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicRefundValueAdapter : IRefundValuePort
    {
        private readonly Dictionary<string, DeterministicRefundValueOperation> _dispatched = new(StringComparer.Ordinal);

        public ProviderOperationOutcome RequestOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool RecoveredAsDispatched { get; set; }

        public bool ThrowOnRequest { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public decimal? AmountOverride { get; set; }

        public int? CurrencyOverride { get; set; }

        public bool OmitValueMovementReference { get; set; }

        public string? DispositionOverride { get; set; }

        public List<RefundValueRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched.Keys;

        public Task<RefundValueResult> RequestAsync(
            RefundValueRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowOnRequest)
                throw new InvalidOperationException("The refund value movement provider is unreachable.");

            var recorded = Remember(request);

            if (ThrowAfterDispatch)
                throw new InvalidOperationException("The refund value movement response never reached Ordering.");

            return Task.FromResult(new RefundValueResult(
                recorded.Outcome,
                recorded.ValueMovementReference,
                null,
                recorded.Amount,
                recorded.CurrencyId,
                recorded.Disposition));
        }

        public Task<RefundValueRecovery> RecoverAsync(
            RefundValueRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The refund value movement provider is unreachable.");

            if (RecoveredAsDispatched)
                return Task.FromResult(new RefundValueRecovery(
                    true,
                    RecoveryOutcome,
                    RecoveryOutcome == ProviderOperationOutcome.Confirmed
                        ? $"VAL-RECOVERED-{request.OperationId}"
                        : null));

            if (!_dispatched.TryGetValue(request.OperationKey, out var dispatched))
                return Task.FromResult(new RefundValueRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such refund value operation"));

            var resolved = dispatched.Resolved(RecoveryOutcome);

            _dispatched[request.OperationKey] = resolved;

            return Task.FromResult(new RefundValueRecovery(
                true,
                resolved.Outcome,
                resolved.ValueMovementReference,
                null,
                resolved.Amount,
                resolved.CurrencyId,
                resolved.Disposition));
        }

        private DeterministicRefundValueOperation Remember(RefundValueRequest request)
        {
            var intent = Intent(request);

            if (_dispatched.TryGetValue(request.OperationKey, out var existing))
                return string.Equals(existing.Intent, intent, StringComparison.Ordinal)
                    ? existing
                    : throw new InvalidOperationException(
                        $"A different refund value intent already owns operation key {request.OperationKey}.");

            var recorded = new DeterministicRefundValueOperation(
                intent,
                RequestOutcome,
                RequestOutcome != ProviderOperationOutcome.Rejected && !OmitValueMovementReference
                    ? $"VAL-{request.DocumentNumber}"
                    : null,
                AmountOverride ?? request.ApprovedAmount,
                CurrencyOverride ?? request.CurrencyId,
                DispositionOverride ?? request.ApprovedDisposition);

            _dispatched[request.OperationKey] = recorded;

            return recorded;
        }

        private static string Intent(RefundValueRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.DocumentNumber,
                request.SuccessorDocumentNumber,
                request.ApprovedAmount,
                request.CurrencyId,
                request.ApprovedDisposition,
                request.DispositionReference);
    }
}
