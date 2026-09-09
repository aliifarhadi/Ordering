using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Operations
{
    public interface IOrderOperationCoordinator
    {
        Task<OrderOperation> BeginAsync(
            long orderId,
            ServicingOperationKind kind,
            string idempotencyKey,
            object requestIntent,
            int? expectedCommercialVersion = null,
            CancellationToken cancellationToken = default);

        Task ResolveAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken = default);

        string ProviderOperationKey(OrderOperation operation, string step);

        string Fingerprint(object requestIntent);
    }
}
