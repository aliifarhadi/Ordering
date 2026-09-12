namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public interface IExchangeFundingPort
    {
        Task<ExchangeFundingResult> GuaranteeAsync(
            ExchangeFundingGuaranteeRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingRecovery> RecoverGuaranteeAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingResult> CaptureAsync(
            ExchangeFundingCaptureRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingRecovery> RecoverCaptureAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingResult> ReleaseAsync(
            ExchangeFundingReleaseRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingRecovery> RecoverReleaseAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
