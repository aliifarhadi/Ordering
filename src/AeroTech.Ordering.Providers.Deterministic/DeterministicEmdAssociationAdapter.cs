using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.EmdAssociation;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicEmdAssociationAdapter : IEmdAssociationPort
    {
        private readonly Dictionary<string, DeterministicEmdAssociationOperation> _dispatched =
            new(StringComparer.Ordinal);

        public ProviderOperationOutcome ReassociateOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public Dictionary<string, ProviderOperationOutcome> OutcomeByCoupon { get; } = new(StringComparer.Ordinal);

        public HashSet<string> ThrowAfterDispatchForCoupons { get; } = new(StringComparer.Ordinal);

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public bool OmitProviderReference { get; set; }

        public string? ReportedEmdDocumentNumber { get; set; }

        public int? ReportedEmdCouponNumber { get; set; }

        public string? ReportedAssociatedDocumentNumber { get; set; }

        public int? ReportedAssociatedCouponNumber { get; set; }

        public List<EmdReassociationRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched.Keys;

        public Task<EmdAssociationResult> ReassociateAsync(
            EmdReassociationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedRequests.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The reassociation request never left Ordering.");

            var recorded = Remember(request);

            if (ThrowAfterDispatch || ThrowAfterDispatchForCoupons.Contains(Key(request)))
                throw new InvalidOperationException("The reassociation response never reached Ordering.");

            return Task.FromResult(AsResult(recorded));
        }

        public Task<EmdAssociationRecovery> RecoverReassociationAsync(
            EmdAssociationRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The reassociation provider is unreachable.");

            if (!_dispatched.TryGetValue(request.OperationKey, out var dispatched))
                return Task.FromResult(new EmdAssociationRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such reassociation operation"));

            var resolved = dispatched.Resolved(RecoveryOutcome);

            _dispatched[request.OperationKey] = resolved;

            return Task.FromResult(new EmdAssociationRecovery(
                true,
                resolved.Outcome,
                resolved.ProviderReference,
                resolved.EmdDocumentNumber,
                resolved.EmdCouponNumber,
                resolved.AssociatedDocumentNumber,
                resolved.AssociatedCouponNumber));
        }

        private DeterministicEmdAssociationOperation Remember(EmdReassociationRequest request)
        {
            var intent = Intent(request);

            if (_dispatched.TryGetValue(request.OperationKey, out var existing))
                return string.Equals(existing.Intent, intent, StringComparison.Ordinal)
                    ? existing
                    : throw new InvalidOperationException(
                        $"A different reassociation intent already owns operation key {request.OperationKey}.");

            var outcome = OutcomeByCoupon.TryGetValue(Key(request), out var overridden)
                ? overridden
                : ReassociateOutcome;

            var settled = outcome != ProviderOperationOutcome.Rejected;

            var recorded = new DeterministicEmdAssociationOperation(
                intent,
                outcome,
                settled && !OmitProviderReference ? $"ASSOC-{Reference(request.OperationKey)}" : null,
                ReportedEmdDocumentNumber ?? request.EmdDocumentNumber,
                ReportedEmdCouponNumber ?? request.EmdCouponNumber,
                ReportedAssociatedDocumentNumber ?? request.SuccessorDocumentNumber,
                ReportedAssociatedCouponNumber ?? request.SuccessorCouponNumber);

            _dispatched[request.OperationKey] = recorded;

            return recorded;
        }

        private static EmdAssociationResult AsResult(DeterministicEmdAssociationOperation recorded)
            => new(
                recorded.Outcome,
                recorded.ProviderReference,
                recorded.EmdDocumentNumber,
                recorded.EmdCouponNumber,
                recorded.AssociatedDocumentNumber,
                recorded.AssociatedCouponNumber);

        private static string Intent(EmdReassociationRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.EmdDocumentNumber,
                request.EmdCouponNumber,
                request.PredecessorDocumentNumber,
                request.PredecessorCouponNumber,
                request.SuccessorDocumentNumber,
                request.SuccessorCouponNumber,
                request.BeneficiaryTravellerId,
                request.IssuerCarrierId,
                request.DecisionReference);

        private static string Key(EmdReassociationRequest request)
            => $"{request.EmdDocumentNumber}:{request.EmdCouponNumber}";

        private static string Reference(string operationKey) => operationKey.Replace(':', '-');
    }
}
