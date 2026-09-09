using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public interface IReservationReleaseCoordinator
    {
        Task<ProviderOperationOutcome> ReleaseAsync(
            long orderId,
            OrderOperation operation,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default);

        Task<ProviderOperationOutcome> RecoverAsync(
            long orderId,
            OrderOperation operation,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default);

        Task<bool> HasOutstandingObligationAsync(
            long orderId,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default);
    }
}
