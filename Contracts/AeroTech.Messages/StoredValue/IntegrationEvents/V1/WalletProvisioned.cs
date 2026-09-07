namespace AeroTech.Messages.StoredValue.IntegrationEvents.V1
{
    public sealed record WalletProvisioned(
        long WalletId,
        long? OwnerCustomerId,
        long? OwnerOrganizationId,
        long IssuerLegalEntityId,
        string WalletPurpose) : BaseIntegrationEvent;
}
