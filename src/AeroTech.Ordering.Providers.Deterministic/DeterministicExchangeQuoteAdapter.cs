using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicExchangeQuoteAdapter : IExchangeQuotePort
    {
        private readonly Dictionary<string, AcceptedExchange> _accepted = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AcceptedQuotedExchangeSelection> _selectionsByKey = new(StringComparer.Ordinal);
        private readonly List<ExchangeQuote> _quotes = new();

        public List<ExchangeQuoteRequest> ObservedQuoteRequests { get; } = new();

        public List<AcceptedQuotedExchangeSelection> ObservedSelections { get; } = new();

        public bool ThrowOnAccept { get; set; }

        public void Quote(ExchangeQuote quote, AcceptedExchange accepted)
        {
            _quotes.Add(quote);
            _accepted[accepted.QuotedExchangeId] = accepted;
        }

        public Task<ExchangeQuote> QuoteAsync(ExchangeQuoteRequest request, CancellationToken cancellationToken = default)
        {
            ObservedQuoteRequests.Add(request);

            var quote = _quotes.LastOrDefault(candidate =>
                candidate.PredecessorOrderServiceId == request.PredecessorOrderServiceId);

            return quote is not null
                ? Task.FromResult(quote)
                : throw ExceptionFactory.OrderExchangeRequiresQuote(request.OrderId);
        }

        public Task<AcceptedExchange> AcceptQuotedExchangeAsync(
            AcceptedQuotedExchangeSelection selection,
            CancellationToken cancellationToken = default)
        {
            ObservedSelections.Add(selection);

            if (_selectionsByKey.TryGetValue(selection.OperationKey, out var prior) && prior != selection)
                throw new InvalidOperationException("A different exchange acceptance already owns this operation key.");

            _selectionsByKey[selection.OperationKey] = selection;

            if (ThrowOnAccept)
                throw new InvalidOperationException("The exchange acceptance response never reached Ordering.");

            return _accepted.TryGetValue(selection.QuotedExchangeId, out var accepted)
                ? Task.FromResult(accepted)
                : throw ExceptionFactory.OrderExchangeRequiresQuote(selection.OrderId);
        }
    }
}
