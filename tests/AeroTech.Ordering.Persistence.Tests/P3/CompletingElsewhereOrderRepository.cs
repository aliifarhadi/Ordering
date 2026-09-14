using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed class CompletingElsewhereOrderRepository : IOrderRepository
    {
        private readonly IOrderRepository _orders;
        private readonly Func<Task> _completeElsewhere;

        public CompletingElsewhereOrderRepository(IOrderRepository orders, Func<Task> completeElsewhere)
        {
            _orders = orders;
            _completeElsewhere = completeElsewhere;
        }

        public int Completions { get; private set; }

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => _orders.AddAsync(order, cancellationToken);

        public async Task<Order?> GetAsync(long id, CancellationToken cancellationToken = default)
        {
            var snapshot = await _orders.GetAsync(id, cancellationToken);

            if (Completions == 0)
            {
                Completions++;
                await _completeElsewhere();
            }

            return snapshot;
        }

        public Task<long?> FindCustomerIdAsync(long id, CancellationToken cancellationToken = default)
            => _orders.FindCustomerIdAsync(id, cancellationToken);

        public Task<bool> RecordLocatorExistsAsync(string recordLocator, CancellationToken cancellationToken = default)
            => _orders.RecordLocatorExistsAsync(recordLocator, cancellationToken);

        public Task<IReadOnlyList<long>> GetExpiredOrderIdsAsync(
            DateTimeOffset now,
            int batchSize,
            CancellationToken cancellationToken = default)
            => _orders.GetExpiredOrderIdsAsync(now, batchSize, cancellationToken);
    }
}
