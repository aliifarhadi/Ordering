using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed class CompetingConfirmationEvidenceStore : IServicingExternalEvidenceStore
    {
        public const string CompetingReference = "COMPETING-CONFIRMATION";
        public const string CompetingDetail = "a competing worker confirmed first";

        private readonly IServicingExternalEvidenceStore _durable;

        public CompetingConfirmationEvidenceStore(IServicingExternalEvidenceStore durable) => _durable = durable;

        public bool Competed { get; private set; }

        public async Task<ServicingEvidenceRecording> RecordAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind = null,
            string? documentNumber = null,
            CancellationToken cancellationToken = default)
        {
            if (outcome == ProviderOperationOutcome.Confirmed && !Competed)
            {
                Competed = true;

                await _durable.RecordAsync(
                    operationId,
                    stage,
                    ProviderOperationOutcome.Confirmed,
                    CompetingReference,
                    CompetingDetail,
                    documentKind,
                    documentNumber,
                    cancellationToken);
            }

            return await _durable.RecordAsync(
                operationId, stage, outcome, providerReference, detail, documentKind, documentNumber, cancellationToken);
        }

        public Task<IReadOnlyList<ServicingExternalEvidence>> ListAsync(
            long operationId,
            CancellationToken cancellationToken = default)
            => _durable.ListAsync(operationId, cancellationToken);
    }
}
