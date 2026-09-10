using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain.Ports.ChangeQuote;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicChangeQuoteAdapter : IChangeQuotePort
    {
        private readonly Dictionary<string, AcceptedVoluntaryChange> _accepted = new(StringComparer.Ordinal);
        private readonly List<ChangeQuote> _quotes = new();

        public List<ChangeQuoteRequest> ObservedQuoteRequests { get; } = new();

        public List<AcceptedQuotedChangeSelection> ObservedSelections { get; } = new();

        public bool ThrowOnAccept { get; set; }

        public void Quote(ChangeQuote quote, AcceptedVoluntaryChange accepted)
        {
            _quotes.Add(quote);
            _accepted[accepted.QuotedChangeId] = accepted;
        }

        public Task<ChangeQuote> QuoteAsync(ChangeQuoteRequest request, CancellationToken cancellationToken = default)
        {
            ObservedQuoteRequests.Add(request);

            var quote = _quotes.LastOrDefault(candidate =>
                candidate.ReplacedOrderServiceId == request.OrderServiceId);

            return quote is not null
                ? Task.FromResult(quote)
                : throw ExceptionFactory.OrderChangeRequiresQuote(request.OrderId);
        }

        public Task<AcceptedVoluntaryChange> AcceptQuotedChangeAsync(
            AcceptedQuotedChangeSelection selection,
            CancellationToken cancellationToken = default)
        {
            ObservedSelections.Add(selection);

            if (ThrowOnAccept)
                throw new InvalidOperationException("The change acceptance response never reached Ordering.");

            return _accepted.TryGetValue(selection.QuotedChangeId, out var accepted)
                ? Task.FromResult(accepted)
                : throw ExceptionFactory.OrderChangeRequiresQuote(selection.OrderId);
        }
    }
}
