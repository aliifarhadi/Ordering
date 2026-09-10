using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicDocumentExchangeAdapter : IDocumentExchangePort
    {
        private readonly Dictionary<string, DocumentExchangeRequest> _dispatched = new(StringComparer.Ordinal);

        public DocumentExchangeEligibilityOutcome EligibilityOutcome { get; set; } = DocumentExchangeEligibilityOutcome.Eligible;

        public ProviderOperationOutcome ExchangeOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool ThrowOnEligibility { get; set; }

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public bool ReportSuccessorWhilePending { get; set; }

        public string? SuccessorDocumentNumber { get; set; }

        public string? RecoveredSuccessorDocumentNumber { get; set; }

        public int SuccessorCouponNumber { get; set; } = 1;

        public long IssuerCarrierId { get; set; } = 1;

        public long? IssuingOfficeId { get; set; }

        public DocumentAuthority Authority { get; set; } = DocumentAuthority.Local;

        public List<DocumentExchangeEligibilityRequest> ObservedEligibilityRequests { get; } = new();

        public List<DocumentExchangeRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched.Keys;

        public Task<DocumentExchangeEligibility> CheckEligibilityAsync(
            DocumentExchangeEligibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedEligibilityRequests.Add(request);

            if (ThrowOnEligibility)
                throw new InvalidOperationException("The document exchange eligibility response never reached Ordering.");

            return Task.FromResult(new DocumentExchangeEligibility(EligibilityOutcome));
        }

        public Task<DocumentExchangeResult> ExchangeAsync(
            DocumentExchangeRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The document exchange request never left Ordering.");

            _dispatched[request.OperationKey] = request;

            if (ThrowAfterDispatch)
                throw new InvalidOperationException("The document exchange response never reached Ordering.");

            return Task.FromResult(new DocumentExchangeResult(
                ExchangeOutcome,
                ExchangeOutcome == ProviderOperationOutcome.Rejected ? null : ProviderReferenceFor(request.PredecessorDocumentNumber),
                ReportsSuccessor(ExchangeOutcome)
                    ? Successor(SuccessorDocumentNumber ?? StableSuccessorNumber(request.OperationId))
                    : null));
        }

        public Task<DocumentExchangeRecovery> RecoverAsync(
            DocumentExchangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The document exchange provider is unreachable.");

            if (!_dispatched.ContainsKey(request.OperationKey))
                return Task.FromResult(new DocumentExchangeRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such document exchange operation"));

            return Task.FromResult(new DocumentExchangeRecovery(
                true,
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Rejected ? null : ProviderReferenceFor(request.PredecessorDocumentNumber),
                ReportsSuccessor(RecoveryOutcome)
                    ? Successor(RecoveredSuccessorDocumentNumber
                                ?? SuccessorDocumentNumber
                                ?? StableSuccessorNumber(request.OperationId))
                    : null));
        }

        private bool ReportsSuccessor(ProviderOperationOutcome outcome)
            => outcome == ProviderOperationOutcome.Confirmed
               || (ReportSuccessorWhilePending
                   && outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown);

        private SuccessorDocumentIdentity Successor(string documentNumber)
            => new(documentNumber, SuccessorCouponNumber, IssuerCarrierId, IssuingOfficeId, Authority, null);

        private static string StableSuccessorNumber(long operationId) => $"EXC{operationId}";

        private static string ProviderReferenceFor(string predecessorDocumentNumber) => $"EXCH-{predecessorDocumentNumber}";
    }
}
