using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Queries.QuoteExchange
{
    public sealed record QuoteExchangeQuery(
        long OrderId,
        long PredecessorOrderServiceId) : IRequest<ExchangeQuoteOutcome>;
}
