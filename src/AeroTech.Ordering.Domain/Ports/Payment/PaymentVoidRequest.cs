namespace AeroTech.Ordering.Domain.Providers.Payment
{
    public sealed record PaymentVoidRequest(
        string IdempotencyKey,
        long OrderId,
        string ProviderReference,
        decimal Amount);
}
