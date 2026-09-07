using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder
{
    public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, long>
    {
        private readonly IOfferProvider _offerProvider;
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderQueryDbSynchronizer _orderSynchronizer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CreateOrderCommandHandler(
            IOfferProvider offerProvider,
            IOrderRepository orderRepository,
            IOrderQueryDbSynchronizer orderSynchronizer,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _offerProvider = offerProvider;
            _orderRepository = orderRepository;
            _orderSynchronizer = orderSynchronizer;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<long> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
        {
            // 1. Resolve offer from AirPrice / Offer provider
            var offer = await _offerProvider.GetByOfferIdAsync(command.OfferId, cancellationToken);

            // 2. ACL maps provider offer model to clean domain args
            // 3. Domain factory creates valid Order
            var order = Order.Create(MapToArgs(command), offer, _idGenerator, _clock);

             // 4. Persist
            await _orderRepository.AddAsync(order, cancellationToken);

            var created = order.GetEvents().OfType<OrderCreated>().Single();
            await _orderSynchronizer.ProjectCreatedAsync(order.ToReadModelSnapshot(created.TimeOfOccurrence), cancellationToken);

            // 5. Save
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 6. Return PNR-less created summary
            return order.Id;
        }

        private static CreateOrderArgs MapToArgs(CreateOrderCommand command)
            => CreateOrderArgsMapper.Map(
                command.CustomerId,
                command.Channel,
                command.CreatorUserId,
                command.AirlineOfficeId,
                command.OfferId,
                command.CommissionRate,
                command.Travellers,
                command.Contact,
                command.SeatSelections);
    }
}
