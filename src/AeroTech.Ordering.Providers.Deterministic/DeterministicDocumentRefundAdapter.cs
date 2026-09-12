using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentRefundAdapter : IDocumentRefundPort
    {
        private readonly Dictionary<string, IReadOnlyList<int>> _dispatched = new(StringComparer.Ordinal);

        public EligibilityOutcome Eligibility { get; set; } = EligibilityOutcome.Allowed;

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

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
            ObservedEligibilityKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentRefundEligibility(Eligibility));
        }

        public Task<DocumentRefundResult> RefundAsync(
            DocumentRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRefundKeys.Add(request.OperationKey);
            ObservedRefundRequests.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The document refund request never left Ordering.");

            _dispatched[request.OperationKey] = request.CouponNumbers;

            if (ThrowAfterDispatch)
                throw new InvalidOperationException("The document refund response never reached Ordering.");

            return Task.FromResult(new DocumentRefundResult(
                RefundOutcome,
                RefundOutcome == ProviderOperationOutcome.Rejected ? null : $"RFND-{request.DocumentNumber}",
                null,
                ReportedDocumentNumber ?? request.DocumentNumber,
                ReportedCouponNumbers ?? request.CouponNumbers));
        }

        public Task<DocumentRefundRecovery> RecoverAsync(
            DocumentRefundRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The document refund provider is unreachable.");

            if (!_dispatched.TryGetValue(request.OperationKey, out var dispatched))
                return Task.FromResult(new DocumentRefundRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such document refund operation"));

            return Task.FromResult(new DocumentRefundRecovery(
                true,
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Rejected ? null : $"RFND-{request.DocumentNumber}",
                DocumentNumber: request.DocumentNumber,
                CouponNumbers: dispatched));
        }
    }
}
