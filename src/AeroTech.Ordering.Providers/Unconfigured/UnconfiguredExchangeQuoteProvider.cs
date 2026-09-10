using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Exchange.Services
{
    public sealed class UnconfiguredExchangeQuoteProvider : IExchangeQuotePort
    {
        public Task<ExchangeQuote> QuoteAsync(ExchangeQuoteRequest request, CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeQuoteSourceNotConfigured();

        public Task<AcceptedExchange> AcceptQuotedExchangeAsync(
            AcceptedQuotedExchangeSelection selection,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeQuoteSourceNotConfigured();
    }
}
