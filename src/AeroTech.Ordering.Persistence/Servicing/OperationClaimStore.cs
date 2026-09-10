using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class OperationClaimStore : IOperationClaimStore
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public OperationClaimStore(OrderingDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        {
            _dbContext = dbContext;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<OperationClaim> AcquireAsync(
            long orderId,
            long operationId,
            DateTimeOffset recoveryLeaseUntil,
            CancellationToken cancellationToken = default)
        {
            OperationsWriteBoundary.EnsureNoPendingDomainState(_dbContext);

            var existing = await BlockingClaimQuery(orderId).SingleOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                if (existing.OperationId != operationId)
                    throw ExceptionFactory.OperationInProgress(existing.OperationId, orderId);

                existing.Generation++;
                existing.RecoveryLeaseUntil = recoveryLeaseUntil;

                try
                {
                    await OperationsWriteBoundary.SaveAsync(_dbContext, cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _dbContext.Entry(existing).State = EntityState.Detached;
                    throw ExceptionFactory.OperationClaimConcurrentlyAcquired(orderId);
                }

                return Project(existing);
            }

            var claim = new OperationOrderClaim
            {
                Id = _idGenerator.NewId(),
                OperationId = operationId,
                OrderId = orderId,
                Generation = 1,
                IsBlocking = true,
                AcquiredAt = _clock.GetDateTime(),
                RecoveryLeaseUntil = recoveryLeaseUntil
            };

            _dbContext.Set<OperationOrderClaim>().Add(claim);

            try
            {
                await OperationsWriteBoundary.SaveAsync(_dbContext, cancellationToken);
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(claim).State = EntityState.Detached;

                var winner = await BlockingClaimQuery(orderId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

                if (winner is null)
                    throw;

                if (winner.OperationId != operationId)
                    throw ExceptionFactory.OperationInProgress(winner.OperationId, orderId);

                throw ExceptionFactory.OperationClaimConcurrentlyAcquired(orderId);
            }

            return Project(claim);
        }

        public async Task<OperationClaim?> FindBlockingAsync(long orderId, CancellationToken cancellationToken = default)
        {
            var claim = await BlockingClaimQuery(orderId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            return claim is null ? null : Project(claim);
        }

        public async Task EnsureCurrentGenerationAsync(
            long orderId,
            long operationId,
            long generation,
            CancellationToken cancellationToken = default)
        {
            var claim = await BlockingClaimQuery(orderId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (claim is null || claim.OperationId != operationId)
                throw ExceptionFactory.OperationClaimNotHeld(operationId, orderId);

            if (claim.Generation != generation)
                throw ExceptionFactory.OperationClaimGenerationStale(orderId, claim.Generation, generation);
        }

        public async Task ResolveAsync(
            long orderId,
            long operationId,
            long generation,
            CancellationToken cancellationToken = default)
        {
            var claim = await BlockingClaimQuery(orderId).SingleOrDefaultAsync(cancellationToken);

            if (claim is null || claim.OperationId != operationId)
                throw ExceptionFactory.OperationClaimNotHeld(operationId, orderId);

            if (claim.Generation != generation)
                throw ExceptionFactory.OperationClaimGenerationStale(orderId, claim.Generation, generation);

            claim.IsBlocking = false;
            claim.ResolvedAt = _clock.GetDateTime();
        }

        private IQueryable<OperationOrderClaim> BlockingClaimQuery(long orderId)
            => _dbContext.Set<OperationOrderClaim>().Where(claim => claim.OrderId == orderId && claim.IsBlocking);

        private static OperationClaim Project(OperationOrderClaim claim)
            => new(claim.OperationId, claim.OrderId, claim.Generation, claim.RecoveryLeaseUntil);
    }
}
