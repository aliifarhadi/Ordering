namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record StoredValueCreditCreated(
        long WalletId,
        long WalletTransactionId,
        string FundClass,
        decimal Amount,
        int CurrencyId,
        string AccountingClassification) : BaseIntegrationEvent;
}
