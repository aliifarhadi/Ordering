using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Payment
{
    public sealed record PaymentCaptureRequest(
        string IdempotencyKey,
        long OrderId,
        string OrderReference,
        decimal Amount,
        int CurrencyId,
        FormOfPayment FormOfPayment,
        string? WalletReference);
}
