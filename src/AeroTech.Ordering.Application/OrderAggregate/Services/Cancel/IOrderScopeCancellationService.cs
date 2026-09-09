namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public interface IOrderScopeCancellationService
    {
        Task<ScopeCancellationOutcome> CancelItemAsync(
            long orderId,
            long orderItemId,
            string quotedCancellationId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);

        Task<ScopeCancellationOutcome> RemoveServicesAsync(
            long orderId,
            IReadOnlyList<long> orderServiceIds,
            string quotedCancellationId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }
}
