using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicExchangeQuoteAdapter : IExchangeQuotePort
    {
        private readonly Dictionary<string, AcceptedExchange> _accepted = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AcceptedQuotedExchangeSelection> _selectionsByKey = new(StringComparer.Ordinal);

        public Func<ExchangeQuoteRequest, AcceptedExchange>? Composer { get; set; }

        public List<ExchangeQuoteRequest> ObservedQuoteRequests { get; } = new();

        public List<AcceptedQuotedExchangeSelection> ObservedSelections { get; } = new();

        public bool ThrowOnAccept { get; set; }

        public AcceptedExchange? Accepted(string quotedExchangeId)
            => _accepted.TryGetValue(quotedExchangeId, out var accepted) ? accepted : null;

        public void Reshape(string quotedExchangeId, Func<AcceptedExchange, AcceptedExchange> shape)
            => _accepted[quotedExchangeId] = shape(_accepted[quotedExchangeId]);

        public void Prime(AcceptedExchange accepted) => _accepted[accepted.QuotedExchangeId] = accepted;

        public Task<ExchangeQuote> QuoteAsync(ExchangeQuoteRequest request, CancellationToken cancellationToken = default)
        {
            ObservedQuoteRequests.Add(request);

            if (Composer is null)
                throw ExceptionFactory.OrderExchangeRequiresQuote(request.OrderId);

            var accepted = Composer(request);

            _accepted[accepted.QuotedExchangeId] = accepted;

            return Task.FromResult(new ExchangeQuote(
                accepted.SourceSystem,
                accepted.QuotedExchangeId,
                accepted.TargetSelectionRef,
                accepted.PricingSource,
                accepted.OrderId,
                accepted.ExpectedCommercialVersion,
                accepted.SaleCurrencyId,
                accepted.PredecessorElectronicTicketId,
                accepted.ChangedOrderServiceIds,
                accepted.Coupons,
                accepted.MonetaryOutcome,
                accepted.PricingLines,
                accepted.ExpiresAt,
                accepted.SourcePricingReference));
        }

        public Task<AcceptedExchange> AcceptQuotedExchangeAsync(
            AcceptedQuotedExchangeSelection selection,
            CancellationToken cancellationToken = default)
        {
            ObservedSelections.Add(selection);

            if (_selectionsByKey.TryGetValue(selection.OperationKey, out var prior) && !SameIntent(prior, selection))
                throw new InvalidOperationException("A different exchange acceptance already owns this operation key.");

            _selectionsByKey[selection.OperationKey] = selection;

            if (ThrowOnAccept)
                throw new InvalidOperationException("The exchange acceptance response never reached Ordering.");

            return _accepted.TryGetValue(selection.QuotedExchangeId, out var accepted)
                ? Task.FromResult(accepted)
                : throw ExceptionFactory.OrderExchangeRequiresQuote(selection.OrderId);
        }

        private static bool SameIntent(AcceptedQuotedExchangeSelection prior, AcceptedQuotedExchangeSelection selection)
            => prior.OrderId == selection.OrderId
               && prior.OperationId == selection.OperationId
               && prior.QuotedExchangeId == selection.QuotedExchangeId
               && prior.ExpectedCommercialVersion == selection.ExpectedCommercialVersion
               && prior.PredecessorElectronicTicketId == selection.PredecessorElectronicTicketId
               && prior.SaleCurrencyId == selection.SaleCurrencyId
               && prior.ChangedOrderServiceIds.SequenceEqual(selection.ChangedOrderServiceIds);
    }
}
