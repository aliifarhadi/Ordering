using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.FulfillmentTaskAggregate
{
    public sealed class FulfillmentTaskRepository : IFulfillmentTaskRepository
    {
        private readonly OrderingDbContext _dbContext;

        public FulfillmentTaskRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(FulfillmentTask task, CancellationToken cancellationToken = default)
            => await _dbContext.Set<FulfillmentTask>().AddAsync(task, cancellationToken);

        public Task<FulfillmentTask?> GetAsync(long id, CancellationToken cancellationToken = default)
            => _dbContext.Set<FulfillmentTask>()
                .Include(task => task.Targets)
                .Include(task => task.Attempts)
                .FirstOrDefaultAsync(task => task.Id == id, cancellationToken);

        public Task<FulfillmentTask?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => _dbContext.Set<FulfillmentTask>()
                .FirstOrDefaultAsync(task => task.IdempotencyKey == idempotencyKey, cancellationToken);

        public async Task<IReadOnlyList<long>> GetDueTaskIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
            => await _dbContext.Set<FulfillmentTask>()
                .Where(task => task.Status == OrderFulfillmentStatus.Pending && (task.NextRetryAt == null || task.NextRetryAt <= now))
                .OrderBy(task => task.Sequence)
                .ThenBy(task => task.Id)
                .Take(batchSize)
                .Select(task => task.Id)
                .ToListAsync(cancellationToken);

        public Task<FulfillmentTask?> GetByOrderAndTypeAsync(long orderId, OrderFulfillmentTaskType taskType, CancellationToken cancellationToken = default)
            => _dbContext.Set<FulfillmentTask>()
                .Include(task => task.Targets)
                .Where(task => task.OrderId == orderId && task.TaskType == taskType)
                .OrderByDescending(task => task.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<FulfillmentTask>> GetAllByOrderAndTypeAsync(long orderId, OrderFulfillmentTaskType taskType, CancellationToken cancellationToken = default)
            => await _dbContext.Set<FulfillmentTask>()
                .Include(task => task.Targets)
                .Where(task => task.OrderId == orderId && task.TaskType == taskType)
                .OrderBy(task => task.Id)
                .ToListAsync(cancellationToken);
    }
}
