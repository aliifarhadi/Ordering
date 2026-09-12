using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicEmdExchangeAdapter : IEmdExchangePort
    {
        private readonly Dictionary<string, DeterministicEmdExchangeOperation> _dispatched =
            new(StringComparer.Ordinal);

        public long IssuerCarrierId { get; set; } = 1L;

        public long? IssuingOfficeId { get; set; }

        public DocumentAuthority Authority { get; set; } = DocumentAuthority.Local;

        public ProviderOperationOutcome ExchangeOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public bool OmitProviderReference { get; set; }

        public bool OmitSuccessor { get; set; }

        public string? SuccessorDocumentNumberOverride { get; set; }

        public ElectronicMiscDocumentType? SuccessorTypeOverride { get; set; }

        public int? SuccessorCurrencyOverride { get; set; }

        public decimal? SuccessorValueOverride { get; set; }

        public string? SuccessorSubCodeOverride { get; set; }

        public int? SuccessorAssociatedCouponOverride { get; set; }

        public bool OmitSuccessorAssociation { get; set; }

        public bool OmitCoupledResidual { get; set; }

        public bool ReportUnexpectedResidual { get; set; }

        public long? BeneficiaryOverride { get; set; }

        public IReadOnlyList<int>? SuccessorCouponNumbersOverride { get; set; }

        public bool OmitSuccessorTicketDocument { get; set; }

        public bool ForceSuccessorTicketDocument { get; set; }

        public ResidualInstrumentKind? ResidualInstrumentOverride { get; set; }

        public decimal? ResidualAmountOverride { get; set; }

        public int? ResidualCurrencyOverride { get; set; }

        public bool OmitResidualReasonForIssuance { get; set; }

        public string ResidualReasonForIssuanceCode { get; set; } = "D";

        public string ResidualReasonForIssuanceSubCode { get; set; } = "98R";

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched.Keys;

        public List<EmdExchangeRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public Task<EmdExchangeResult> ExchangeAsync(
            EmdExchangeRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedRequests.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The miscellaneous document exchange never left Ordering.");

            var recorded = Remember(request);

            if (ThrowAfterDispatch)
                throw new InvalidOperationException(
                    "The miscellaneous document exchange response never reached Ordering.");

            return Task.FromResult(new EmdExchangeResult(
                recorded.Outcome,
                recorded.ProviderReference,
                recorded.Successor,
                null,
                recorded.Residual));
        }

        public Task<EmdExchangeRecovery> RecoverAsync(
            EmdExchangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException(
                    "The miscellaneous document exchange authority is unreachable.");

            if (!_dispatched.TryGetValue(request.OperationKey, out var dispatched))
                return Task.FromResult(new EmdExchangeRecovery(
                    false,
                    ProviderOperationOutcome.Unknown,
                    Detail: "no such miscellaneous document exchange operation"));

            var resolved = dispatched.Resolved(RecoveryOutcome);

            _dispatched[request.OperationKey] = resolved;

            return Task.FromResult(new EmdExchangeRecovery(
                true,
                resolved.Outcome,
                resolved.ProviderReference,
                resolved.Successor,
                null,
                resolved.Residual));
        }

        private DeterministicEmdExchangeOperation Remember(EmdExchangeRequest request)
        {
            var intent = Intent(request);

            if (_dispatched.TryGetValue(request.OperationKey, out var existing))
                return string.Equals(existing.Intent, intent, StringComparison.Ordinal)
                    ? existing
                    : throw new InvalidOperationException(
                        "A different miscellaneous document exchange intent already owns operation key "
                        + request.OperationKey
                        + ".");

            var confirmed = ExchangeOutcome == ProviderOperationOutcome.Confirmed;

            var recorded = new DeterministicEmdExchangeOperation(
                intent,
                ExchangeOutcome,
                ExchangeOutcome == ProviderOperationOutcome.Rejected || OmitProviderReference
                    ? null
                    : $"EMDX-{request.SourceDocumentNumber}",
                confirmed && !OmitSuccessor ? Successor(request) : null,
                confirmed ? Residual(request) : null);

            _dispatched[request.OperationKey] = recorded;

            return recorded;
        }

        private SuccessorEmdIdentity Successor(EmdExchangeRequest request)
        {
            var type = SuccessorTypeOverride ?? request.SuccessorType;
            var couponNumber = 1;

            return new SuccessorEmdIdentity(
                SuccessorDocumentNumberOverride ?? NextDocumentNumber(request),
                type,
                IssuerCarrierId,
                IssuingOfficeId,
                Authority,
                request.SuccessorReasonForIssuanceCode,
                SuccessorCurrencyOverride ?? request.CurrencyId,
                request.SuccessorCoupons
                    .Select((coupon, index) => new SuccessorEmdCouponIdentity(
                        SuccessorCouponNumbersOverride is { } overridden && overridden.Count > index
                            ? overridden[index]
                            : couponNumber++,
                        coupon.Purpose,
                        SuccessorSubCodeOverride ?? coupon.ReasonForIssuanceSubCode,
                        SuccessorValueOverride ?? coupon.Value,
                        SuccessorCurrencyOverride ?? request.CurrencyId,
                        type == ElectronicMiscDocumentType.Associated && !OmitSuccessorAssociation
                            ? SuccessorAssociatedCouponOverride ?? coupon.TargetSuccessorTicketCouponNumber
                            : null))
                    .ToList(),
                BeneficiaryOverride ?? request.BeneficiaryTravellerId,
                ForceSuccessorTicketDocument
                    ? request.SuccessorTicketDocumentNumber ?? "T-FORCED"
                    : type == ElectronicMiscDocumentType.Associated
                      && !OmitSuccessorAssociation
                      && !OmitSuccessorTicketDocument
                        ? request.SuccessorTicketDocumentNumber
                        : null);
        }

        private ResidualDocumentIdentity? Residual(EmdExchangeRequest request)
        {
            if (ReportUnexpectedResidual)
                return new ResidualDocumentIdentity(
                    $"{NextDocumentNumber(request)}R",
                    ResidualInstrumentKind.Emd,
                    1m,
                    request.CurrencyId,
                    IssuerCarrierId,
                    IssuingOfficeId,
                    Authority,
                    ResidualReasonForIssuanceCode,
                    ResidualReasonForIssuanceSubCode);

            if (request.Residual is not { } residual || OmitCoupledResidual)
                return null;

            return new ResidualDocumentIdentity(
                $"{NextDocumentNumber(request)}R",
                ResidualInstrumentOverride ?? residual.ExpectedInstrument,
                ResidualAmountOverride ?? residual.Amount,
                ResidualCurrencyOverride ?? residual.CurrencyId,
                IssuerCarrierId,
                IssuingOfficeId,
                Authority,
                OmitResidualReasonForIssuance ? string.Empty : ResidualReasonForIssuanceCode,
                OmitResidualReasonForIssuance ? string.Empty : ResidualReasonForIssuanceSubCode);
        }

        private static string NextDocumentNumber(EmdExchangeRequest request)
            => $"{request.SourceDocumentNumber}X";

        private static string Intent(EmdExchangeRequest request)
            => string.Join(
                '|',
                request.OrderId,
                request.OperationId,
                request.ExchangeGroupRef,
                request.SourceDocumentNumber,
                string.Join(',', request.SourceCouponNumbers.Order()),
                request.BeneficiaryTravellerId,
                (int)request.SuccessorType,
                request.SuccessorReasonForIssuanceCode,
                request.CurrencyId,
                request.SuccessorTicketDocumentNumber,
                request.DecisionReference,
                string.Join(
                    ',',
                    request.SuccessorCoupons.Select(coupon => string.Join(
                        '~',
                        (int)coupon.Purpose,
                        coupon.ReasonForIssuanceSubCode,
                        coupon.Value,
                        coupon.TargetSuccessorTicketCouponNumber))),
                request.SourcePricingReference,
                request.Residual?.Amount,
                request.Residual?.CurrencyId,
                request.Residual?.Disposition,
                (int?)request.Residual?.ExpectedInstrument);
    }
}
