using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderService
{
    public sealed class AddOrderServiceCommandHandler : IRequestHandler<AddOrderServiceCommand, OrderChangeOutcome>
    {
        private readonly IOrderChangeService _orderChangeService;

        public AddOrderServiceCommandHandler(IOrderChangeService orderChangeService) => _orderChangeService = orderChangeService;

        public Task<OrderChangeOutcome> Handle(AddOrderServiceCommand command, CancellationToken cancellationToken)
            => _orderChangeService.AddServiceAsync(command.OrderId, command.AcceptSelectedQuotedOfferList, command.IdempotencyKey, command.ExpectedCommercialVersion, cancellationToken);
    }
}
