using AeroTech.Ordering.Domain.Ports.ExchangeResidual;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredExchangeResidualProvider : IExchangeResidualValuePort
    {
        public Task<ExchangeResidualResult> FulfillAsync(
            ExchangeResidualRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeResidualSourceNotConfigured();

        public Task<ExchangeResidualRecovery> RecoverAsync(
            ExchangeResidualRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeResidualSourceNotConfigured();
    }
}
