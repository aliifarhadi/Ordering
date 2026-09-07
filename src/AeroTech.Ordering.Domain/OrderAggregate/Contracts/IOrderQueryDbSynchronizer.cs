using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Ordering.Domain.OrderAggregate.Contracts
{
    public interface IOrderQueryDbSynchronizer : IQueryDbSynchronizer
    {
        Task ProjectCreatedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectReservedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectReservationUnconfirmedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectReserveFailedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectPaidAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectPaymentFailedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectPaymentUnconfirmedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectTicketedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectTicketingFailedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectVoidedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectCancelledAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectExpiredAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectSplitAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);

        Task ProjectTimeToLiveUpdatedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default);
    }
}
