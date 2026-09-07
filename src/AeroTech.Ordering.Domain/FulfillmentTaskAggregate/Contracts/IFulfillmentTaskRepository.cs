namespace AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts
{
    public interface IFulfillmentTaskRepository
    {
        Task AddAsync(FulfillmentTask task, CancellationToken cancellationToken = default);

        Task<FulfillmentTask?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task<FulfillmentTask?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<long>> GetDueTaskIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);

        Task<FulfillmentTask?> GetByOrderAndTypeAsync(long orderId, Messages.Ordering.Enums.OrderFulfillmentTaskType taskType, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FulfillmentTask>> GetAllByOrderAndTypeAsync(long orderId, Messages.Ordering.Enums.OrderFulfillmentTaskType taskType, CancellationToken cancellationToken = default);
    }
}
