using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentRefundAdapter : IDocumentRefundPort
    {
        private readonly Dictionary<string, DeterministicDocumentRefundOperation> _dispatched =
            new(StringComparer.Ordinal);

        public EligibilityOutcome Eligibility { get; set; } = EligibilityOutcome.Allowed;

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public bool OmitProviderReference { get; set; }

        public string? ReportedDocumentNumber { get; set; }

        public IReadOnlyList<int>? ReportedCouponNumbers { get; set; }

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched.Keys;

        public ProviderOperationOutcome RefundOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public List<string> ObservedEligibilityKeys { get; } = new();

        public List<string> ObservedRefundKeys { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public List<DocumentRefundRequest> ObservedRefundRequests { get; } = new();

        public Task<DocumentRefundEligibility> CheckEligibilityAsync(
            DocumentRefundEligibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedEligibilityKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentRefundEligibility(Eligibility));
        }

        public Task<DocumentRefundResult> RefundAsync(
            DocumentRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedRefundKeys.Add(request.OperationKey);
            ObservedRefundRequests.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The document refund request never left Ordering.");

            var recorded = Remember(request);

            if (ThrowAfterDispatch)
                throw new InvalidOperationException("The document refund response never reached Ordering.");

            return Task.FromResult(new DocumentRefundResult(
                recorded.Outcome,
                recorded.ProviderReference,
                null,
                recorded.DocumentNumber,
                recorded.CouponNumbers));
        }

        public Task<DocumentRefundRecovery> RecoverAsync(
            DocumentRefundRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The document refund provider is unreachable.");

            if (!_dispatched.TryGetValue(request.OperationKey, out var dispatched))
                return Task.FromResult(new DocumentRefundRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such document refund operation"));

            var resolved = dispatched.Resolved(RecoveryOutcome);

            _dispatched[request.OperationKey] = resolved;

            return Task.FromResult(new DocumentRefundRecovery(
                true,
                resolved.Outcome,
                resolved.ProviderReference,
                null,
                resolved.DocumentNumber,
                resolved.CouponNumbers));
        }

        private DeterministicDocumentRefundOperation Remember(DocumentRefundRequest request)
        {
            var intent = Intent(request);

            if (_dispatched.TryGetValue(request.OperationKey, out var existing))
                return string.Equals(existing.Intent, intent, StringComparison.Ordinal)
                    ? existing
                    : throw new InvalidOperationException(
                        $"A different document refund intent already owns operation key {request.OperationKey}.");

            var recorded = new DeterministicDocumentRefundOperation(
                intent,
                RefundOutcome,
                RefundOutcome == ProviderOperationOutcome.Rejected || OmitProviderReference
                    ? null
                    : $"RFND-{request.DocumentNumber}",
                ReportedDocumentNumber ?? request.DocumentNumber,
                ReportedCouponNumbers ?? request.CouponNumbers);

            _dispatched[request.OperationKey] = recorded;

            return recorded;
        }

        private static string Intent(DocumentRefundRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.DocumentNumber,
                string.Join(',', request.CouponNumbers.Order()));
    }
}
