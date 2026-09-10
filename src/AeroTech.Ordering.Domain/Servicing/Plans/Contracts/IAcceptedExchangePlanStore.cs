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

        Task RecordDocumentExchangeOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            SuccessorDocumentIdentity? successor,
            string? detail,
            CancellationToken cancellationToken = default);
    }
}
