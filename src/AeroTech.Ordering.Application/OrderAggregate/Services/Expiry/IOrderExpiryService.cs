using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Expiry
{
    public interface IOrderExpiryService
    {
        Task<OrderStatus> ExpireAsync(long orderId, CancellationToken cancellationToken = default);
    }
}
