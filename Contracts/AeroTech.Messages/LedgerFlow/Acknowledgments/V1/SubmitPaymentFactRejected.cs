namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    // Rejected acknowledgment of JetPay's SubmitPaymentFactCommand: permanent refusal, with RejectionCode.
    // Type identity (namespace + name) is the contract — it must match JetPay's copy.
    public sealed record SubmitPaymentFactRejected(
        long TenantId,
        long PaymentAccountingFactSetId,
        string Destination,
        string RejectionCode) : BaseAcknowledgeCommand;
}
