using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class CommandReceiptStore : ICommandReceiptStore
    {
        private readonly OrderingDbContext _dbContext;
        private readonly IHomeOperatorProvider _homeOperatorProvider;
        private readonly ICallerContext _callerContext;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CommandReceiptStore(
            OrderingDbContext dbContext,
            IHomeOperatorProvider homeOperatorProvider,
            ICallerContext callerContext,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _dbContext = dbContext;
            _homeOperatorProvider = homeOperatorProvider;
            _callerContext = callerContext;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<CommandReceiptResult> AcquireAsync(
            string operationName,
            string idempotencyKey,
            string requestHash,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
            ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

            OperationsWriteBoundary.EnsureNoPendingDomainState(_dbContext);

            var ownerAirlineId = await _homeOperatorProvider.GetOwnerAirlineIdAsync(cancellationToken);
            var callerScope = CallerScope.For(_callerContext);

            var existing = await FindAsync(ownerAirlineId, callerScope, operationName, idempotencyKey, cancellationToken);

            if (existing is not null)
                return Replay(existing, requestHash);

            var now = _clock.GetDateTime();

            var receipt = new CommandReceipt
            {
                Id = _idGenerator.NewId(),
                OwnerAirlineId = ownerAirlineId,
                CallerScope = callerScope,
                OperationName = operationName,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                OperationId = _idGenerator.NewId(),
                Status = CommandReceiptStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Set<CommandReceipt>().Add(receipt);

            try
            {
                await OperationsWriteBoundary.SaveAsync(_dbContext, cancellationToken);
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(receipt).State = EntityState.Detached;

                var winner = await FindAsync(ownerAirlineId, callerScope, operationName, idempotencyKey, cancellationToken);

                if (winner is null)
                    throw;

                return Replay(winner, requestHash);
            }

            return Project(receipt, isReplay: false);
        }

        public async Task AttachOrderAsync(long receiptId, long orderId, CancellationToken cancellationToken = default)
        {
            var receipt = await _dbContext.Set<CommandReceipt>()
                .SingleOrDefaultAsync(candidate => candidate.Id == receiptId, cancellationToken);

            if (receipt is null || receipt.OrderId == orderId)
                return;

            receipt.OrderId = orderId;
            receipt.UpdatedAt = _clock.GetDateTime();
        }

        public async Task SetStatusAsync(long receiptId, CommandReceiptStatus status, CancellationToken cancellationToken = default)
        {
            var receipt = await _dbContext.Set<CommandReceipt>()
                .SingleOrDefaultAsync(candidate => candidate.Id == receiptId, cancellationToken);

            if (receipt is null || receipt.Status == status)
                return;

            receipt.Status = status;
            receipt.UpdatedAt = _clock.GetDateTime();
        }

        private Task<CommandReceipt?> FindAsync(
            long ownerAirlineId,
            string callerScope,
            string operationName,
            string idempotencyKey,
            CancellationToken cancellationToken)
            => _dbContext.Set<CommandReceipt>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    receipt => receipt.OwnerAirlineId == ownerAirlineId
                               && receipt.CallerScope == callerScope
                               && receipt.OperationName == operationName
                               && receipt.IdempotencyKey == idempotencyKey,
                    cancellationToken);

        private static CommandReceiptResult Replay(CommandReceipt existing, string requestHash)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw ExceptionFactory.IdempotencyPayloadConflict(existing.IdempotencyKey, existing.OperationName);

            return Project(existing, isReplay: true);
        }

        private static CommandReceiptResult Project(CommandReceipt receipt, bool isReplay)
            => new(
                receipt.Id,
                receipt.OwnerAirlineId,
                receipt.CallerScope,
                receipt.OperationName,
                receipt.IdempotencyKey,
                receipt.Status,
                isReplay,
                receipt.OrderId,
                receipt.OperationId!.Value);
    }
}
