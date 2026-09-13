using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingManualResolutionStore : IServicingManualResolutionStore
    {
        private readonly OrderingDbContext _context;

        public ServicingManualResolutionStore(OrderingDbContext context) => _context = context;

        public async Task<ServicingManualResolution?> FindAsync(
            long operationId,
            string resolutionId,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Set<ServicingManualResolutionRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    resolution => resolution.OperationId == operationId
                        && resolution.ResolutionId == resolutionId,
                    cancellationToken);

            return row is null ? null : Map(row);
        }

        public async Task<ServicingManualResolution> AppendAsync(
            ServicingManualResolution resolution,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.Set<ServicingManualResolutionRow>()
                .SingleOrDefaultAsync(
                    row => row.OperationId == resolution.OperationId
                        && row.ResolutionId == resolution.ResolutionId,
                    cancellationToken);

            if (existing is not null)
                return Map(existing);

            await _context.Set<ServicingManualResolutionRow>().AddAsync(
                new ServicingManualResolutionRow
                {
                    OperationId = resolution.OperationId,
                    ResolutionId = resolution.ResolutionId,
                    Kind = resolution.Kind,
                    Actor = resolution.Actor,
                    Reason = resolution.Reason,
                    Reference = resolution.Reference,
                    EvidenceStage = resolution.EvidenceStage,
                    ExpectedClaimGeneration = resolution.ExpectedClaimGeneration,
                    RecordedAt = resolution.RecordedAt
                },
                cancellationToken);

            return resolution;
        }

        public async Task<IReadOnlyList<ServicingManualResolution>> ListAsync(
            long operationId,
            CancellationToken cancellationToken = default)
            => await _context.Set<ServicingManualResolutionRow>()
                .AsNoTracking()
                .Where(resolution => resolution.OperationId == operationId)
                .OrderBy(resolution => resolution.RecordedAt)
                .ThenBy(resolution => resolution.ResolutionId)
                .Select(resolution => new ServicingManualResolution(
                    resolution.OperationId,
                    resolution.ResolutionId,
                    resolution.Kind,
                    resolution.Actor,
                    resolution.Reason,
                    resolution.Reference,
                    resolution.EvidenceStage,
                    resolution.ExpectedClaimGeneration,
                    resolution.RecordedAt))
                .ToListAsync(cancellationToken);

        private static ServicingManualResolution Map(ServicingManualResolutionRow row)
            => new(
                row.OperationId,
                row.ResolutionId,
                row.Kind,
                row.Actor,
                row.Reason,
                row.Reference,
                row.EvidenceStage,
                row.ExpectedClaimGeneration,
                row.RecordedAt);
    }
}
