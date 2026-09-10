namespace AeroTech.Ordering.Domain.Ports.Payment
{
    public sealed record PaymentCaptureResult(
        string PaymentReference,
        DateTimeOffset CapturedAt);
}
