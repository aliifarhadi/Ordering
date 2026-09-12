using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Providers.Deterministic
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

        public bool OmitSuccessorCoupons { get; set; }

        public bool DuplicatePredecessorCouponMapping { get; set; }

        public bool DuplicateSuccessorCouponNumber { get; set; }

        public int? UnknownPredecessorCouponNumber { get; set; }

        public string? SuccessorDocumentNumber { get; set; }

        public bool OmitCoupledResidualDocument { get; set; }

        public bool ReturnUnrequestedResidualDocument { get; set; }

        public string? ResidualDocumentNumberOverride { get; set; }

        public decimal? ResidualAmountOverride { get; set; }

        public int? ResidualCurrencyOverride { get; set; }

        public ResidualInstrumentKind? ResidualInstrumentOverride { get; set; }

        public string ResidualReasonForIssuanceCode { get; set; } = "R";

        public string ResidualReasonForIssuanceSubCode { get; set; } = "0B5";

        public string? RecoveredSuccessorDocumentNumber { get; set; }

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
                    ? Successor(SuccessorDocumentNumber ?? StableSuccessorNumber(request.OperationId), request)
                    : null,
                null,
                CoupledResidual(ExchangeOutcome, request)));
        }

        public Task<DocumentExchangeRecovery> RecoverAsync(
            DocumentExchangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The document exchange provider is unreachable.");

            if (!_dispatched.TryGetValue(request.OperationKey, out var dispatched))
                return Task.FromResult(new DocumentExchangeRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such document exchange operation"));

            return Task.FromResult(new DocumentExchangeRecovery(
                true,
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Rejected ? null : ProviderReferenceFor(request.PredecessorDocumentNumber),
                ReportsSuccessor(RecoveryOutcome)
                    ? Successor(
                        RecoveredSuccessorDocumentNumber ?? SuccessorDocumentNumber ?? StableSuccessorNumber(request.OperationId),
                        dispatched)
                    : null,
                null,
                CoupledResidual(RecoveryOutcome, dispatched)));
        }

        private ResidualDocumentIdentity? CoupledResidual(
            ProviderOperationOutcome outcome,
            DocumentExchangeRequest request)
            => outcome == ProviderOperationOutcome.Confirmed
               && (request.Residual is not null || ReturnUnrequestedResidualDocument)
               && !OmitCoupledResidualDocument
                ? new ResidualDocumentIdentity(
                    ResidualDocumentNumberOverride ?? StableResidualNumber(request.OperationId),
                    ResidualInstrumentOverride ?? Family(request.Residual?.ExpectedInstrument ?? ResidualInstrumentKind.Emd),
                    ResidualAmountOverride ?? request.Residual?.Amount ?? UnrequestedResidualAmount,
                    ResidualCurrencyOverride ?? request.Residual?.CurrencyId ?? UnrequestedResidualCurrencyId,
                    IssuerCarrierId,
                    IssuingOfficeId,
                    Authority,
                    ResidualReasonForIssuanceCode,
                    ResidualReasonForIssuanceSubCode,
                    $"RESDOC-{request.OperationId}")
                : null;

        public decimal UnrequestedResidualAmount { get; set; } = 1m;

        public int UnrequestedResidualCurrencyId { get; set; } = 1;

        private static ResidualInstrumentKind Family(ResidualInstrumentKind expected)
            => expected == ResidualInstrumentKind.Unknown ? ResidualInstrumentKind.Emd : expected;

        private static string StableResidualNumber(long operationId) => $"RES{operationId}";

        private bool ReportsSuccessor(ProviderOperationOutcome outcome)
            => outcome == ProviderOperationOutcome.Confirmed
               || (ReportSuccessorWhilePending
                   && outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown);

        private SuccessorDocumentIdentity Successor(string documentNumber, DocumentExchangeRequest request)
            => new(
                documentNumber,
                IssuerCarrierId,
                IssuingOfficeId,
                Authority,
                null,
                SuccessorCoupons(request));

        private IReadOnlyList<SuccessorCouponIdentity> SuccessorCoupons(DocumentExchangeRequest request)
        {
            if (OmitSuccessorCoupons)
                return [];

            var reissued = request.Coupons
                .OrderBy(coupon => coupon.PredecessorCouponNumber)
                .Select((coupon, index) => new SuccessorCouponIdentity(coupon.PredecessorCouponNumber, index + 1))
                .ToList();

            if (UnknownPredecessorCouponNumber is { } unknown)
                reissued[^1] = reissued[^1] with { PredecessorCouponNumber = unknown };

            if (DuplicatePredecessorCouponMapping)
                reissued[^1] = reissued[^1] with { PredecessorCouponNumber = reissued[0].PredecessorCouponNumber };

            if (DuplicateSuccessorCouponNumber)
                reissued[^1] = reissued[^1] with { CouponNumber = reissued[0].CouponNumber };

            return reissued;
        }

        private static string StableSuccessorNumber(long operationId) => $"EXC{operationId}";

        private static string ProviderReferenceFor(string predecessorDocumentNumber) => $"EXCH-{predecessorDocumentNumber}";
    }
}
