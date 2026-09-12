namespace AeroTech.Ordering.Domain.Ports.DocumentRefund
{
    public interface IDocumentRefundPort
    {
        Task<DocumentRefundEligibility> CheckEligibilityAsync(
            DocumentRefundEligibilityRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRefundResult> RefundAsync(
            DocumentRefundRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRefundRecovery> RecoverAsync(
            DocumentRefundRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
