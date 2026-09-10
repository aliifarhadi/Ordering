using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingOperationStore : IServicingOperationStore
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IHomeOperatorProvider _homeOperatorProvider;
        private readonly IClock _clock;

        public ServicingOperationStore(
            OrderingDbContext dbContext,
            IHomeOperatorProvider homeOperatorProvider,
            IClock clock)
        {
            _dbContext = dbContext;
            _homeOperatorProvider = homeOperatorProvider;
            _clock = clock;
        }

        public async Task<ServicingOperationRecord> PrepareAsync(
            long operationId,
            long orderId,
            ServicingOperationKind kind,
            string requestHash,
            long claimGeneration,
            long? commandReceiptId = null,
            int? expectedCommercialVersion = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
            OperationsWriteBoundary.EnsureNoPendingDomainState(_dbContext);

            var existing = await _dbContext.Set<ServicingOperation>()
                .SingleOrDefaultAsync(operation => operation.Id == operationId, cancellationToken);

            if (existing is not null)
            {
                existing.ClaimGeneration = claimGeneration;
                existing.UpdatedAt = _clock.GetDateTime();

                await OperationsWriteBoundary.SaveAsync(_dbContext, cancellationToken);

                return Project(existing);
            }

            var ownerAirlineId = await _homeOperatorProvider.GetOwnerAirlineIdAsync(cancellationToken);
            var now = _clock.GetDateTime();

            var operation = new ServicingOperation
            {
                Id = operationId,
                OwnerAirlineId = ownerAirlineId,
                OrderId = orderId,
                CommandReceiptId = commandReceiptId,
                Kind = kind,
                Status = ServicingOperationStatus.Prepared,
                RequestHash = requestHash,
                ExpectedCommercialVersion = expectedCommercialVersion,
                ClaimGeneration = claimGeneration,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Set<ServicingOperation>().Add(operation);

            try
            {
                await OperationsWriteBoundary.SaveAsync(_dbContext, cancellationToken);
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(operation).State = EntityState.Detached;

                var winner = await _dbContext.Set<ServicingOperation>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.Id == operationId, cancellationToken);

                if (winner is null)
                    throw;

                return Project(winner);
            }

            return Project(operation);
        }

        public async Task TransitionAsync(
            long operationId,
            ServicingOperationStatus status,
            long claimGeneration,
            CancellationToken cancellationToken = default)
        {
            var operation = await _dbContext.Set<ServicingOperation>()
                .SingleOrDefaultAsync(candidate => candidate.Id == operationId, cancellationToken)
                ?? throw ExceptionFactory.ServicingOperationNotFound(operationId);

            operation.Status = status;
            operation.ClaimGeneration = claimGeneration;
            operation.UpdatedAt = _clock.GetDateTime();
        }

        public async Task<ServicingOperationRecord?> FindAsync(long operationId, CancellationToken cancellationToken = default)
        {
            var operation = await _dbContext.Set<ServicingOperation>()
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == operationId, cancellationToken);

            return operation is null ? null : Project(operation);
        }

        private static ServicingOperationRecord Project(ServicingOperation operation)
            => new(
                operation.Id,
                operation.OwnerAirlineId,
                operation.OrderId,
                operation.Kind,
                operation.Status,
                operation.ClaimGeneration);
    }
}
