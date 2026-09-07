namespace AeroTech.Ordering.Domain.Providers.Payment
{
    public sealed record PaymentCaptureResult(
        string PaymentReference,
        DateTimeOffset CapturedAt);
}
