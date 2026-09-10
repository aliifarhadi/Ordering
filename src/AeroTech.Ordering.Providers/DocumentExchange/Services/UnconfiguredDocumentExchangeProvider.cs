using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.DocumentExchange.Services
{
    public sealed class UnconfiguredDocumentExchangeProvider : IDocumentExchangePort
    {
        public Task<DocumentExchangeEligibility> CheckEligibilityAsync(
            DocumentExchangeEligibilityRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentExchangeSourceNotConfigured();

        public Task<DocumentExchangeResult> ExchangeAsync(
            DocumentExchangeRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentExchangeSourceNotConfigured();

        public Task<DocumentExchangeRecovery> RecoverAsync(
            DocumentExchangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentExchangeSourceNotConfigured();
    }
}
