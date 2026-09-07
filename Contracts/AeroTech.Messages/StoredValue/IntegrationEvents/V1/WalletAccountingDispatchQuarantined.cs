namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record WalletAccountingDispatchQuarantined(
        long AccountingFactSetId,
        string Destination,
        int AttemptCount,
        string Reason) : BaseIntegrationEvent;
}
