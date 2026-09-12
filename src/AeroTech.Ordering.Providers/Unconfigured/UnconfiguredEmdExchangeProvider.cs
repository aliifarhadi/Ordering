using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredEmdExchangeProvider : IEmdExchangePort
    {
        public Task<EmdExchangeResult> ExchangeAsync(
            EmdExchangeRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.EmdExchangeSourceNotConfigured();

        public Task<EmdExchangeRecovery> RecoverAsync(
            EmdExchangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.EmdExchangeSourceNotConfigured();
    }
}
