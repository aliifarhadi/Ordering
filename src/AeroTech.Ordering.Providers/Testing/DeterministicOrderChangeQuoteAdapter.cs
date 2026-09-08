using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicOrderChangeQuoteAdapter : IOrderChangeQuoteProvider
    {
        private readonly Dictionary<string, AcceptedAddServiceChange> _quoted = new(StringComparer.Ordinal);

        public List<string> ObservedSelections { get; } = new();

        public int CallCount => ObservedSelections.Count;

        public void Quote(AcceptedAddServiceChange accepted)
            => _quoted[Key(accepted.QuotedOfferId, accepted.SelectedOfferItemId)] = accepted;

        public void Expire(string quotedOfferId, string selectedOfferItemId)
            => _quoted.Remove(Key(quotedOfferId, selectedOfferItemId));

        public Task<AcceptedAddServiceChange> AcceptSelectedQuotedOfferAsync(
            AcceptedQuotedOfferSelection selection,
            CancellationToken cancellationToken = default)
        {
            var key = Key(selection.QuotedOfferId, selection.SelectedOfferItemId);

            ObservedSelections.Add(key);

            return _quoted.TryGetValue(key, out var accepted)
                ? Task.FromResult(accepted)
                : throw ExceptionFactory.AcceptedQuotedOfferNotUsable(key);
        }

        private static string Key(string quotedOfferId, string selectedOfferItemId)
            => $"{quotedOfferId}:{selectedOfferItemId}";
    }
}
