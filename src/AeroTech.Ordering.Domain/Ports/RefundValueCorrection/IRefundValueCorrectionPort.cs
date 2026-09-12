namespace AeroTech.Ordering.Domain.Ports.RefundValueCorrection
{
    public interface IRefundValueCorrectionPort
    {
        Task<RefundValueCorrectionResult> RequestAsync(
            RefundValueCorrectionRequest request,
            CancellationToken cancellationToken = default);

        Task<RefundValueCorrectionRecovery> RecoverAsync(
            RefundValueCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
