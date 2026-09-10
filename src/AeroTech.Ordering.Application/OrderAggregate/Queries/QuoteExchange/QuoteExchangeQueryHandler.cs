using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Queries.QuoteExchange
{
    public sealed class QuoteExchangeQueryHandler : IRequestHandler<QuoteExchangeQuery, ExchangeQuoteOutcome>
    {
        private readonly IExchangeService _exchangeService;

        public QuoteExchangeQueryHandler(IExchangeService exchangeService) => _exchangeService = exchangeService;

        public Task<ExchangeQuoteOutcome> Handle(QuoteExchangeQuery query, CancellationToken cancellationToken)
            => _exchangeService.QuoteAsync(query.OrderId, query.PredecessorOrderServiceId, cancellationToken);
    }
}
