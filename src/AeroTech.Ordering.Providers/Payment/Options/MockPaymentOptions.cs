namespace AeroTech.Ordering.Providers.Payment.Options
{
    public sealed class MockPaymentOptions
    {
        public MockPaymentOutcome DefaultOutcome { get; set; } = MockPaymentOutcome.Capture;
    }

    public enum MockPaymentOutcome
    {
        Capture,
        Decline,
        Retriable,
        Timeout
    }
}
