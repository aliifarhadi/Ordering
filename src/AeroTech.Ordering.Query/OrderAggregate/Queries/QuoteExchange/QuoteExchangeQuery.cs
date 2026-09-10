using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.QuoteExchange
{
    public sealed record QuoteExchangeQuery(
        long OrderId,
        IReadOnlyList<long> ChangedOrderServiceIds) : IRequest<ExchangeQuoteOutcome>;
}
