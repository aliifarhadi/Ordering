namespace AeroTech.Ordering.Domain.Ports.RefundValue
{
    public interface IRefundValuePort
    {
        Task<RefundValueResult> RequestAsync(RefundValueRequest request, CancellationToken cancellationToken = default);

        Task<RefundValueRecovery> RecoverAsync(
            RefundValueRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
