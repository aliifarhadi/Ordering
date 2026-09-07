using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Expiry
{
    public sealed class OrderExpiryService : IOrderExpiryService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;

        public OrderExpiryService(
            IOrderRepository orderRepository,
            IOrderQueryDbSynchronizer synchronizer,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork)
        {
            _orderRepository = orderRepository;
            _synchronizer = synchronizer;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
        }

        public async Task<OrderStatus> ExpireAsync(long orderId, CancellationToken cancellationToken = default)
        {
            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            order.Expire(_idGenerator, _clock);

            await _synchronizer.ProjectExpiredAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return order.Status;
        }
    }
}
