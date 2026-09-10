using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Ports.Refund;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicRefundQuoteAdapter : IRefundQuotePort
    {
        private readonly Dictionary<string, AcceptedRefund> _accepted = new(StringComparer.Ordinal);
        private readonly List<RefundQuote> _quotes = new();

        public List<RefundQuoteRequest> ObservedQuoteRequests { get; } = new();

        public List<AcceptedQuotedRefundSelection> ObservedSelections { get; } = new();

        public int CallCount => ObservedSelections.Count;

        public void Quote(RefundQuote quote, AcceptedRefund accepted)
        {
            _quotes.Add(quote);
            _accepted[accepted.QuotedRefundId] = accepted;
        }

        public void Expire(string quotedRefundId) => _accepted.Remove(quotedRefundId);

        public Task<RefundQuote> QuoteAsync(RefundQuoteRequest request, CancellationToken cancellationToken = default)
        {
            ObservedQuoteRequests.Add(request);

            var quote = _quotes.LastOrDefault(candidate =>
                candidate.ElectronicTicketId == request.ElectronicTicketId
                && candidate.TicketCouponIds.OrderBy(id => id)
                    .SequenceEqual(request.TicketCouponIds.OrderBy(id => id)));

            return quote is not null
                ? Task.FromResult(quote)
                : throw ExceptionFactory.AcceptedQuotedRefundNotUsable(request.DocumentNumber);
        }

        public Task<AcceptedRefund> AcceptQuotedRefundAsync(
            AcceptedQuotedRefundSelection selection,
            CancellationToken cancellationToken = default)
        {
            ObservedSelections.Add(selection);

            return _accepted.TryGetValue(selection.QuotedRefundId, out var accepted)
                ? Task.FromResult(accepted)
                : throw ExceptionFactory.AcceptedQuotedRefundNotUsable(selection.QuotedRefundId);
        }
    }
}
