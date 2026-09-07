namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record StoredValueCreditReversed(
        long WalletId,
        long WalletTransactionId,
        long FundLotId,
        decimal Amount,
        int CurrencyId) : BaseIntegrationEvent;
}
