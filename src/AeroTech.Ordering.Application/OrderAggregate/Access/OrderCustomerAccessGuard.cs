using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Access
{
    public sealed class OrderCustomerAccessGuard : IOrderCustomerAccessGuard
    {
        private readonly IOrderRepository _orders;
        private readonly ICallerContext _caller;

        public OrderCustomerAccessGuard(IOrderRepository orders, ICallerContext caller)
        {
            _orders = orders;
            _caller = caller;
        }

        public long RequireCustomerId()
        {
            if (!_caller.IsAuthenticated)
                throw ExceptionFactory.CustomerContextRequired();

            return _caller.CustomerId ?? throw ExceptionFactory.CustomerContextRequired();
        }

        public async Task EnsureOwnedAsync(long orderId, CancellationToken cancellationToken = default)
        {
            var customerId = RequireCustomerId();

            var ownerCustomerId = await _orders.FindCustomerIdAsync(orderId, cancellationToken);

            if (ownerCustomerId != customerId)
                throw ExceptionFactory.OrderNotFound(orderId);
        }
    }
}
