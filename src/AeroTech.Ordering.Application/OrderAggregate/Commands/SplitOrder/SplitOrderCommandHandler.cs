using AeroTech.Ordering.Application.OrderAggregate.Services.Split;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.SplitOrder
{
    public sealed class SplitOrderCommandHandler : IRequestHandler<SplitOrderCommand, SplitOrderResult>
    {
        private readonly IOrderSplitService _splitService;

        public SplitOrderCommandHandler(IOrderSplitService splitService) => _splitService = splitService;

        public Task<SplitOrderResult> Handle(SplitOrderCommand command, CancellationToken cancellationToken)
            => _splitService.SplitAsync(command.OrderId, command.TravellerIds, cancellationToken);
    }
}
