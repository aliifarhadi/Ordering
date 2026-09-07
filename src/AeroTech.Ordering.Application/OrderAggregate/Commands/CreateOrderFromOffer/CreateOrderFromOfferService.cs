using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer
{
    public sealed class CreateOrderFromOfferService : ICreateOrderFromOfferService
    {
        private readonly IOfferProvider _offerProvider;
        private readonly IOrderRepository _orderRepository;
        private readonly IInlineReservationService _inlineReservation;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CreateOrderFromOfferService(
            IOfferProvider offerProvider,
            IOrderRepository orderRepository,
            IInlineReservationService inlineReservation,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _offerProvider = offerProvider;
            _orderRepository = orderRepository;
            _inlineReservation = inlineReservation;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<CreateOrderFromOfferResult> ExecuteAsync(
            CreateOrderArgs args,
            CancellationToken cancellationToken = default)
        {
            var offer = await _offerProvider.GetByOfferIdAsync(args.OfferId, cancellationToken);

            var order = Order.Create(args, offer, _idGenerator, _clock);
            await _orderRepository.AddAsync(order, cancellationToken);

            var reservationKey = $"reserve:{order.Id}";

            var outcome = await _inlineReservation.ReserveAsync(order, reservationKey, _clock.GetDateTime(), cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateOrderFromOfferResult(
                order.Id,
                outcome.Status,
                outcome.RecordLocator,
                outcome.FailureReason,
                outcome.ReserveTaskId);
        }
    }
}
