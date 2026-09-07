using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public interface IOrderCancelService
    {
        Task<CancelOrderOutcome> CancelAsync(long orderId, long cancelledBy, CancellationToken cancellationToken = default);
    }

    public sealed record CancelOrderOutcome(long OrderId, OrderStatus Status, IReadOnlyList<long> ReleaseTaskIds);
}
