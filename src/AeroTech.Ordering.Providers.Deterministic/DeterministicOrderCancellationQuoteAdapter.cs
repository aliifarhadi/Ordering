using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicOrderCancellationQuoteAdapter : IOrderCancellationQuoteProvider
    {
        private readonly Dictionary<string, AcceptedScopeCancellation> _quoted = new(StringComparer.Ordinal);

        public List<AcceptedQuotedCancellationSelection> ObservedSelections { get; } = new();

        public int CallCount => ObservedSelections.Count;

        public void Quote(AcceptedScopeCancellation accepted)
            => _quoted[accepted.QuotedCancellationId] = accepted;

        public void Expire(string quotedCancellationId) => _quoted.Remove(quotedCancellationId);

        public Task<AcceptedScopeCancellation> AcceptQuotedCancellationAsync(
            AcceptedQuotedCancellationSelection selection,
            CancellationToken cancellationToken = default)
        {
            ObservedSelections.Add(selection);

            return _quoted.TryGetValue(selection.QuotedCancellationId, out var accepted)
                ? Task.FromResult(accepted)
                : throw ExceptionFactory.AcceptedQuotedCancellationNotUsable(selection.QuotedCancellationId);
        }
    }
}
