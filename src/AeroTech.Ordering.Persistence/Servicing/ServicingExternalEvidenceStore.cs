using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingExternalEvidenceStore : IServicingExternalEvidenceStore
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;

        public ServicingExternalEvidenceStore(OrderingDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public async Task RecordAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind = null,
            string? documentNumber = null,
            CancellationToken cancellationToken = default)
        {
            var now = _clock.GetDateTime();

            var existing = await _dbContext.Set<ServicingExternalEvidenceRow>()
                .FirstOrDefaultAsync(
                    row => row.OperationId == operationId && row.Stage == stage, cancellationToken);

            if (existing is null)
            {
                await _dbContext.Set<ServicingExternalEvidenceRow>().AddAsync(
                    new ServicingExternalEvidenceRow
                    {
                        OperationId = operationId,
                        Stage = stage,
                        Outcome = outcome,
                        ProviderReference = providerReference,
                        Detail = detail,
                        DocumentKind = documentKind,
                        DocumentNumber = documentNumber,
                        RecordedAt = now,
                        UpdatedAt = now
                    },
                    cancellationToken);

                return;
            }

            if (existing.Outcome == ProviderOperationOutcome.Confirmed)
                return;

            existing.Outcome = outcome;
            existing.ProviderReference = providerReference ?? existing.ProviderReference;
            existing.Detail = detail ?? existing.Detail;
            existing.DocumentKind = documentKind ?? existing.DocumentKind;
            existing.DocumentNumber = documentNumber ?? existing.DocumentNumber;
            existing.UpdatedAt = now;
        }

        public async Task<IReadOnlyList<ServicingExternalEvidence>> ListAsync(
            long operationId,
            CancellationToken cancellationToken = default)
            => await _dbContext.Set<ServicingExternalEvidenceRow>()
                .AsNoTracking()
                .Where(row => row.OperationId == operationId)
                .OrderBy(row => row.Stage)
                .Select(row => new ServicingExternalEvidence(
                    row.OperationId,
                    row.Stage,
                    row.Outcome,
                    row.ProviderReference,
                    row.Detail,
                    row.DocumentKind,
                    row.DocumentNumber,
                    row.RecordedAt))
                .ToListAsync(cancellationToken);
    }
}
