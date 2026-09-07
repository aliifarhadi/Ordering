namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record WalletLiabilitySnapshot(
        long SnapshotId,
        int CurrencyId,
        long IssuerLegalEntityId,
        decimal AvailableTotal,
        decimal HeldTotal,
        int ValueBookCount,
        DateTimeOffset SnapshotAt) : BaseIntegrationEvent;
}
