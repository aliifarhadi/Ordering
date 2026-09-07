namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record StoredValueSpendRestored(
        long WalletId,
        long WalletTransactionId,
        string PaymentAuthorizationId,
        string PaymentCaptureId,
        decimal Amount,
        int CurrencyId) : BaseIntegrationEvent;
}
