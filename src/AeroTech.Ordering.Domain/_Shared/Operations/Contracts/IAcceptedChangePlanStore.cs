using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain._Shared.Operations.Contracts
{
    public interface IAcceptedChangePlanStore
    {
        Task<AcceptedChangePlan?> FindAsync(long operationId, CancellationToken cancellationToken = default);

        Task SaveAsync(AcceptedChangePlan plan, CancellationToken cancellationToken = default);

        Task RecordEligibilityOutcomeAsync(
            long operationId,
            DocumentChangeEligibilityOutcome outcome,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordRevalidationOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default);

        Task RecordReservationOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? externalReservationRef,
            CancellationToken cancellationToken = default);
    }
}
