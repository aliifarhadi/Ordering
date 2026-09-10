namespace AeroTech.Ordering.Domain.Ports.Payment
{
    public sealed record PaymentVoidResult(
        string ProviderReference,
        DateTimeOffset VoidedAt);
}
