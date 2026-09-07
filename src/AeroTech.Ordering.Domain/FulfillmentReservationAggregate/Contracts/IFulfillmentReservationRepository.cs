namespace AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts
{
    public interface IFulfillmentReservationRepository
    {
        Task<FulfillmentReservation?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task<FulfillmentReservation?> GetByOperationAsync(long operationId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FulfillmentReservation>> ListByOrderAsync(long orderId, CancellationToken cancellationToken = default);

        Task AddAsync(FulfillmentReservation reservation, CancellationToken cancellationToken = default);
    }
}
