using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Contracts
{
    public interface IAcceptedExchangePlanStore
    {
        Task<AcceptedExchangePlan?> FindAsync(long operationId, CancellationToken cancellationToken = default);

        Task SaveAsync(AcceptedExchangePlan plan, CancellationToken cancellationToken = default);

        Task RecordEligibilityOutcomeAsync(
            long operationId,
            DocumentExchangeEligibilityOutcome outcome,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordReservationOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? externalReservationRef,
            CancellationToken cancellationToken = default);

        Task RecordFundingGuaranteeOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordFundingCaptureOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordFundingReleaseOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordRefundDueOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? valueMovementReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordResidualOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? instrumentReference,
            ResidualInstrumentKind? instrument,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryRefundOutcomeAsync(
            long operationId,
            long emdCouponId,
            bool valueMovement,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryRetentionSettledAsync(
            long operationId,
            long emdCouponId,
            DateTimeOffset settledAt,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryExchangeOutcomeAsync(
            long operationId,
            string exchangeGroupRef,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryExchangeSuccessorAsync(
            long operationId,
            string exchangeGroupRef,
            long successorElectronicMiscDocumentId,
            string successorDocumentNumber,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryExchangeConsequenceAsync(
            long operationId,
            string exchangeGroupRef,
            long priceChangeSetId,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryExchangeFundingOutcomeAsync(
            long operationId,
            string exchangeGroupRef,
            bool capture,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryExchangeRefundDueOutcomeAsync(
            long operationId,
            string exchangeGroupRef,
            ProviderOperationOutcome outcome,
            string? valueMovementReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryExchangeResidualOutcomeAsync(
            long operationId,
            string exchangeGroupRef,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? instrumentReference,
            ResidualInstrumentKind? instrument,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryRefundConsequenceAsync(
            long operationId,
            long emdCouponId,
            long priceChangeSetId,
            CancellationToken cancellationToken = default);

        Task RecordAncillaryAssociationOutcomeAsync(
            long operationId,
            long emdCouponId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordDocumentExchangeOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            SuccessorDocumentIdentity? successor,
            string? detail,
            CancellationToken cancellationToken = default);
    }
}
