using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.Ports.ProductAddition;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.ProductAddition.Services
{
    public sealed class UnconfiguredProductAdditionProvider : IAcceptedProductAdditionPort
    {
        public Task<AcceptedProductAddition> GetAcceptedAdditionAsync(
            AcceptedProductAdditionRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ProductAdditionSourceNotConfigured();
    }
}
