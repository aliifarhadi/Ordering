using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer
{
    public sealed class CreateOrderFromOfferCommandHandler : IRequestHandler<CreateOrderFromOfferCommand, CreateOrderFromOfferResult>
    {
        private readonly ICreateOrderFromOfferService _service;

        public CreateOrderFromOfferCommandHandler(ICreateOrderFromOfferService service) => _service = service;

        public Task<CreateOrderFromOfferResult> Handle(CreateOrderFromOfferCommand command, CancellationToken cancellationToken)
            => _service.ExecuteAsync(MapToArgs(command), cancellationToken);

        private static CreateOrderArgs MapToArgs(CreateOrderFromOfferCommand command)
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
