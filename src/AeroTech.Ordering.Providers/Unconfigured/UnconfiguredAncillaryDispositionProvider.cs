using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredAncillaryDispositionProvider : IAncillaryExchangeDispositionPort
    {
        public Task<AncillaryExchangeDispositionResult> DecideAsync(
            AncillaryExchangeDispositionRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.AncillaryDispositionSourceNotConfigured();
    }
}
