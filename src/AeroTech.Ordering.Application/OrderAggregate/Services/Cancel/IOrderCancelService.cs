using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public interface IOrderCancelService
    {
        Task<CancelOrderOutcome> CancelAsync(
            long orderId,
            VoidReason reason,
            long cancelledBy,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }

}
