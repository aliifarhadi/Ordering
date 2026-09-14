using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed class UnreachableEvidenceStore : IServicingExternalEvidenceStore
    {
        private readonly IServicingExternalEvidenceStore _durable;

        public UnreachableEvidenceStore(IServicingExternalEvidenceStore durable) => _durable = durable;

        public Task<ServicingEvidenceRecording> RecordAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind = null,
            string? documentNumber = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The external evidence store could not be reached.");

        public Task<IReadOnlyList<ServicingExternalEvidence>> ListAsync(
            long operationId,
            CancellationToken cancellationToken = default)
            => _durable.ListAsync(operationId, cancellationToken);
    }
}
