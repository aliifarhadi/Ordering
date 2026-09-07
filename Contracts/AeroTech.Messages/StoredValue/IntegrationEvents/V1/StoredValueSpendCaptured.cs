namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record StoredValueSpendCaptured(
        long WalletId,
        long WalletTransactionId,
        string PaymentAuthorizationId,
        string PaymentCaptureId,
        decimal Amount,
        int CurrencyId) : BaseIntegrationEvent;
}
