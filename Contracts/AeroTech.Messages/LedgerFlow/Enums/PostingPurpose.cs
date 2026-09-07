using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum PostingPurpose
    {
        [Display(Name = "Sale Commitment")] SaleCommitment = 1,
        [Display(Name = "Payment Receipt")] PaymentReceipt = 2,
        [Display(Name = "Payment Allocation")] PaymentAllocation = 3,
        [Display(Name = "Revenue Recognition")] RevenueRecognition = 4,
        [Display(Name = "Refund")] Refund = 5,
        [Display(Name = "Settlement")] Settlement = 6,
        [Display(Name = "Reversal")] Reversal = 7,
        [Display(Name = "Adjustment")] Adjustment = 8,

        // Unwinds a sale as a business event (order cancelled). Distinct from Reversal, which is the
        // compensation of a posting made in error and carries JournalType.Reversal + ReversesJournalId.
        [Display(Name = "Cancellation")] Cancellation = 9,

        // Value moving between orders on a split. Two purposes, not one: both sides share the source
        // event id, and the idempotency key is (Tenant, Book, SourceSystem, SourceEventId, Purpose).
        [Display(Name = "Transfer Out")] TransferOut = 10,
        [Display(Name = "Transfer In")] TransferIn = 11
    }
}
