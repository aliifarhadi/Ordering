using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AcceptExchange
{
    public sealed record AcceptExchangeCommand(
        long OrderId,
        long PredecessorOrderServiceId,
        string QuotedExchangeId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<ExchangeOutcome>;
}
