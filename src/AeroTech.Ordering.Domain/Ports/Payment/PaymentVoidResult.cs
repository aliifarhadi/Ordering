namespace AeroTech.Ordering.Domain.Providers.Payment
{
    public sealed record PaymentVoidResult(
        string ProviderReference,
        DateTimeOffset VoidedAt);
}
