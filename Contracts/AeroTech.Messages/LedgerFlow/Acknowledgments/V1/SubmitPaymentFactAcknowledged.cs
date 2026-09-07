namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    // Accepted acknowledgment of JetPay's SubmitPaymentFactCommand: the fact posted (accounted as
    // JournalEntryId). Type identity (namespace + name) is the contract — it must match JetPay's copy.
    public sealed record SubmitPaymentFactAcknowledged(
        long TenantId,
        long PaymentAccountingFactSetId,
        string Destination,
        long JournalEntryId) : BaseAcknowledgeCommand;
}
