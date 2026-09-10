using AeroTech.Messages.Ordering.Enums;

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
        int PredecessorCouponNumber,
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
        int PredecessorCouponNumber,
        string QuotedExchangeId,
        string TargetSelectionRef,
        string? SourcePricingReference,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        string ReplacementFlightNumber,
        DateTimeOffset ReplacementDepartureAt);

    public sealed record DocumentExchangeRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string PredecessorDocumentNumber);

    public sealed record DocumentExchangeResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        SuccessorDocumentIdentity? Successor = null,
        string? Detail = null);

    public sealed record DocumentExchangeRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        SuccessorDocumentIdentity? Successor = null,
        string? Detail = null)
    {
        public DocumentExchangeResult AsResult() => new(Outcome, ProviderReference, Successor, Detail);
    }

    public sealed record SuccessorDocumentIdentity(
        string DocumentNumber,
        int CouponNumber,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        DateTimeOffset? VoidDeadline);
}
