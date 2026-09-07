namespace AeroTech.Ordering.Domain.OrderAggregate.Contracts
{
    public interface IOrderRepository
    {
        Task AddAsync(Order order, CancellationToken cancellationToken = default);

        Task<Order?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task<bool> RecordLocatorExistsAsync(string recordLocator, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<long>> GetExpiredOrderIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
    }
}
