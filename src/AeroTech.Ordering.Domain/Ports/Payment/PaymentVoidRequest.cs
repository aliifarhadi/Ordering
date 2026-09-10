namespace AeroTech.Ordering.Domain.Ports.Payment
{
    public sealed record PaymentVoidRequest(
        string IdempotencyKey,
        long OrderId,
        string ProviderReference,
        decimal Amount);
}
