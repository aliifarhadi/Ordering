namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    // Accepted acknowledgment of StoredValue's WalletAccountingFactSubmitted: the fact posted (accounted as
    // JournalEntryId). Type identity (namespace + name) is the contract — it must match StoredValue's copy.
    public sealed record SubmitWalletFactAcknowledged(
        long TenantId,
        long AccountingFactSetId,
        string Destination,
        long JournalEntryId) : BaseAcknowledgeCommand;
}
