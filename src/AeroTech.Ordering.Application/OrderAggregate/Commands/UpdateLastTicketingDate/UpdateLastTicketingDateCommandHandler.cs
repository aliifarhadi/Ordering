using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Providers.FlightFlow;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.UpdateLastTicketingDate
{
    public sealed class UpdateLastTicketingDateCommandHandler : IRequestHandler<UpdateLastTicketingDateCommand, Unit>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IFlightFlowProvider _flightFlowProvider;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateLastTicketingDateCommandHandler(
            IOrderRepository orderRepository,
            IFlightFlowProvider flightFlowProvider,
            IOrderQueryDbSynchronizer synchronizer,
            IClock clock,
            IUnitOfWork unitOfWork)
        {
            _orderRepository = orderRepository;
            _flightFlowProvider = flightFlowProvider;
            _synchronizer = synchronizer;
            _clock = clock;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(UpdateLastTicketingDateCommand request, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetAsync(request.OrderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(request.OrderId);

            await ExtendProviderHoldsAsync(order, request.LastTicketingDate, cancellationToken);

            order.UpdateTimeToLive(request.LastTicketingDate, _clock);

            await _synchronizer.ProjectTimeToLiveUpdatedAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }

        private async Task ExtendProviderHoldsAsync(Order order, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            var holdBatchIds = order.OrderServices.OfType<OrderAirTransportService>()
                .Where(service => !string.IsNullOrWhiteSpace(service.HoldBatchId))
                .Select(service => service.HoldBatchId!)
                .Distinct();

            foreach (var holdBatchId in holdBatchIds)
            {
                try
                {
                    await _flightFlowProvider.ExtendHeldAsync(new ExtendHeldSeatsRequest(holdBatchId, expiresAt), cancellationToken);
                }
                catch (ProviderRequestException exception)
                {
                    throw ExceptionFactory.ProviderRequestFailed(exception.Message);
                }
            }
        }
    }
}
