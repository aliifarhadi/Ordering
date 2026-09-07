using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Backoffice
{
    public sealed class BackofficeCreateOrderFromOfferCommandHandler : IRequestHandler<BackofficeCreateOrderFromOfferCommand, CreateOrderFromOfferResult>
    {
        private const SalesChannel BackofficeChannel = SalesChannel.BackOffice;

        private readonly ICreateOrderFromOfferService _service;

        public BackofficeCreateOrderFromOfferCommandHandler(ICreateOrderFromOfferService service) => _service = service;

        public Task<CreateOrderFromOfferResult> Handle(BackofficeCreateOrderFromOfferCommand command, CancellationToken cancellationToken)
            => _service.ExecuteAsync(MapToArgs(command), cancellationToken);

        private static CreateOrderArgs MapToArgs(BackofficeCreateOrderFromOfferCommand command)
            => CreateOrderArgsMapper.Map(
                command.CustomerId,
                BackofficeChannel,
                command.CreatorUserId,
                command.AirlineOfficeId,
                command.OfferId,
                command.CommissionRate,
                command.Travellers,
                command.Contact,
                command.SeatSelections);
    }
}
