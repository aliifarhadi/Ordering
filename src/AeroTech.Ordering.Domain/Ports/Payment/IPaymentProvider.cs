namespace AeroTech.Ordering.Domain.Ports.Payment
{
    public interface IPaymentProvider
    {
        Task<PaymentCaptureResult> CaptureAsync(PaymentCaptureRequest request, CancellationToken cancellationToken = default);

        Task<PaymentVoidResult> VoidAsync(PaymentVoidRequest request, CancellationToken cancellationToken = default);
    }
}
