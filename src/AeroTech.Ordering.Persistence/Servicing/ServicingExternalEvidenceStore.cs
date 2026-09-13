using System.Data.Common;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingExternalEvidenceStore : IServicingExternalEvidenceStore
    {
        private const int UpsertAttempts = 3;

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

            for (var attempt = 0; attempt < UpsertAttempts; attempt++)
            {
                if (await UpgradeAsync(
                        operationId, stage, outcome, providerReference, detail,
                        documentKind, documentNumber, now, cancellationToken))
                    return;

                if (await IsConfirmedAsync(operationId, stage, cancellationToken))
                    return;

                if (await InsertAsync(
                        operationId, stage, outcome, providerReference, detail,
                        documentKind, documentNumber, now, cancellationToken))
                    return;
            }

            throw ExceptionFactory.ServicingEvidenceNotRecorded(operationId, stage);
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

        private async Task<bool> UpgradeAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind,
            string? documentNumber,
            DateTimeOffset now,
            CancellationToken cancellationToken)
            => await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE [Order].[ServicingExternalEvidences]
                      SET [Outcome] = {(int)outcome},
                          [ProviderReference] = COALESCE({providerReference}, [ProviderReference]),
                          [Detail] = COALESCE({detail}, [Detail]),
                          [DocumentKind] = COALESCE({(int?)documentKind}, [DocumentKind]),
                          [DocumentNumber] = COALESCE({documentNumber}, [DocumentNumber]),
                          [UpdatedAt] = {now}
                    WHERE [OperationId] = {operationId}
                      AND [Stage] = {(int)stage}
                      AND [Outcome] <> {(int)ProviderOperationOutcome.Confirmed}",
                cancellationToken) > 0;

        private async Task<bool> IsConfirmedAsync(
            long operationId,
            ServicingEvidenceStage stage,
            CancellationToken cancellationToken)
            => await _dbContext.Set<ServicingExternalEvidenceRow>()
                .AsNoTracking()
                .AnyAsync(
                    row => row.OperationId == operationId
                           && row.Stage == stage
                           && row.Outcome == ProviderOperationOutcome.Confirmed,
                    cancellationToken);

        private async Task<bool> RowExistsAsync(
            long operationId,
            ServicingEvidenceStage stage,
            CancellationToken cancellationToken)
            => await _dbContext.Set<ServicingExternalEvidenceRow>()
                .AsNoTracking()
                .AnyAsync(row => row.OperationId == operationId && row.Stage == stage, cancellationToken);

        private async Task<bool> InsertAsync(
            long operationId,
            ServicingEvidenceStage stage,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            AccountableDocumentKind? documentKind,
            string? documentNumber,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO [Order].[ServicingExternalEvidences]
                           ([OperationId], [Stage], [Outcome], [ProviderReference], [Detail],
                            [DocumentKind], [DocumentNumber], [RecordedAt], [UpdatedAt])
                       SELECT {operationId}, {(int)stage}, {(int)outcome}, {providerReference}, {detail},
                              {(int?)documentKind}, {documentNumber}, {now}, {now}
                        WHERE NOT EXISTS (
                              SELECT 1 FROM [Order].[ServicingExternalEvidences]
                               WHERE [OperationId] = {operationId} AND [Stage] = {(int)stage})",
                    cancellationToken) > 0;
            }
            catch (DbException)
            {
                if (await RowExistsAsync(operationId, stage, cancellationToken))
                    return false;

                throw;
            }
        }
    }
}
