using AeroTech.Ordering.Domain.Ports.ExchangeFunding;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredExchangeFundingProvider : IExchangeFundingPort
    {
        public Task<ExchangeFundingResult> GuaranteeAsync(
            ExchangeFundingGuaranteeRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeFundingSourceNotConfigured();

        public Task<ExchangeFundingRecovery> RecoverGuaranteeAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeFundingSourceNotConfigured();

        public Task<ExchangeFundingResult> CaptureAsync(
            ExchangeFundingCaptureRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeFundingSourceNotConfigured();

        public Task<ExchangeFundingRecovery> RecoverCaptureAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeFundingSourceNotConfigured();

        public Task<ExchangeFundingResult> ReleaseAsync(
            ExchangeFundingReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeFundingSourceNotConfigured();

        public Task<ExchangeFundingRecovery> RecoverReleaseAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ExchangeFundingSourceNotConfigured();
    }
}
