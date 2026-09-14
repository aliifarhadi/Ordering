using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts
{
    public interface IServicingExternalEvidenceStore
    {
        Task<ServicingEvidenceRecording> RecordAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind = null,
            string? documentNumber = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ServicingExternalEvidence>> ListAsync(
            long operationId,
            CancellationToken cancellationToken = default);
    }
}
