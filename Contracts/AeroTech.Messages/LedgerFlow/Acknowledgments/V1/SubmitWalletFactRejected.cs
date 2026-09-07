namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    // Rejected acknowledgment of StoredValue's WalletAccountingFactSubmitted: permanent refusal, with
    // RejectionCode. Type identity (namespace + name) is the contract — it must match StoredValue's copy.
    public sealed record SubmitWalletFactRejected(
        long TenantId,
        long AccountingFactSetId,
        string Destination,
        string RejectionCode) : BaseAcknowledgeCommand;
}
