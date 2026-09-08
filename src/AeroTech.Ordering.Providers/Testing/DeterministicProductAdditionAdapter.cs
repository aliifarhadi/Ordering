using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.Ports.ProductAddition;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicProductAdditionAdapter : IAcceptedProductAdditionPort
    {
        private readonly Dictionary<string, AcceptedProductAddition> _accepted = new(StringComparer.Ordinal);

        public List<string> ObservedSourceReferences { get; } = new();

        public int CallCount => ObservedSourceReferences.Count;

        public void Publish(string sourceReference, AcceptedProductAddition addition)
            => _accepted[sourceReference] = addition;

        public void Withdraw(string sourceReference) => _accepted.Remove(sourceReference);

        public Task<AcceptedProductAddition> GetAcceptedAdditionAsync(
            AcceptedProductAdditionRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedSourceReferences.Add(request.SourceReference);

            return _accepted.TryGetValue(request.SourceReference, out var addition)
                ? Task.FromResult(addition)
                : throw ExceptionFactory.AcceptedProductAdditionNotUsable(request.SourceReference);
        }
    }
}
