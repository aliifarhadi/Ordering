using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AcceptExchange
{
    public sealed class AcceptExchangeCommandHandler : IRequestHandler<AcceptExchangeCommand, ExchangeOutcome>
    {
        private readonly IExchangeService _exchangeService;

        public AcceptExchangeCommandHandler(IExchangeService exchangeService) => _exchangeService = exchangeService;

        public Task<ExchangeOutcome> Handle(AcceptExchangeCommand command, CancellationToken cancellationToken)
            => _exchangeService.ExchangeAsync(
                new ExchangeExecution(
                    command.OrderId,
                    command.ChangedOrderServiceIds,
                    command.QuotedExchangeId,
                    command.IdempotencyKey,
                    command.ExpectedCommercialVersion,
                    command.FundingMethodRef),
                cancellationToken);
    }
}
