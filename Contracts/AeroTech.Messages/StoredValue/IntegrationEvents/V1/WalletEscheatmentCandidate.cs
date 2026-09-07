namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record WalletEscheatmentCandidate(
        long WalletId,
        int CurrencyId,
        decimal AvailableTotal,
        DateTimeOffset? LastActivityAt) : BaseIntegrationEvent;
}
