using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public interface IDocumentExchangePort
    {
        Task<DocumentExchangeEligibility> CheckEligibilityAsync(
            DocumentExchangeEligibilityRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentExchangeResult> ExchangeAsync(
            DocumentExchangeRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentExchangeRecovery> RecoverAsync(
            DocumentExchangeRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record DocumentExchangeEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string PredecessorDocumentNumber,
        IReadOnlyList<int> PredecessorCouponNumbers,
        string TargetSelectionRef,
        string? SourcePricingReference);

    public sealed record DocumentExchangeEligibility(
        DocumentExchangeEligibilityOutcome Outcome,
        string? Detail = null);

    public sealed record DocumentExchangeRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string PredecessorDocumentNumber,
        string QuotedExchangeId,
        string TargetSelectionRef,
        string? SourcePricingReference,
        IReadOnlyList<DocumentExchangeCouponRequest> Coupons,
        ExchangeCoupledResidualRequest? Residual = null);

    public sealed record ExchangeCoupledResidualRequest(
        decimal Amount,
        int CurrencyId,
        string Disposition,
        ResidualInstrumentKind ExpectedInstrument);

    public sealed record DocumentExchangeCouponRequest(
        int PredecessorCouponNumber,
        ExchangeCouponDisposition Disposition,
        TicketedSegmentSnapshot Segment);

    public sealed record DocumentExchangeRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string PredecessorDocumentNumber);

    public sealed record DocumentExchangeResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        SuccessorDocumentIdentity? Successor = null,
        string? Detail = null,
        ResidualDocumentIdentity? Residual = null);

    public sealed record DocumentExchangeRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        SuccessorDocumentIdentity? Successor = null,
        string? Detail = null,
        ResidualDocumentIdentity? Residual = null)
    {
        public DocumentExchangeResult AsResult() => new(Outcome, ProviderReference, Successor, Detail, Residual);
    }

    public sealed record ResidualDocumentIdentity(
        string DocumentNumber,
        ResidualInstrumentKind Instrument,
        decimal Amount,
        int CurrencyId,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        string ReasonForIssuanceCode,
        string ReasonForIssuanceSubCode,
        string? ProviderReference = null);

    public sealed record SuccessorDocumentIdentity(
        string DocumentNumber,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        DateTimeOffset? VoidDeadline,
        IReadOnlyList<SuccessorCouponIdentity> Coupons);

    public sealed record SuccessorCouponIdentity(
        int PredecessorCouponNumber,
        int CouponNumber);
}
