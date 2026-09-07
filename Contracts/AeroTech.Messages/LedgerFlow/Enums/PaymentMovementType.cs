using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    // The ledger's interpretation of a JetPay PaymentAccountingFactType. TenderReference records an
    // internal-tender leg that is already accounted elsewhere (wallet / credit), so it posts no cash.
    public enum PaymentMovementType
    {
        [Display(Name = "Capture")] Capture = 1,
        [Display(Name = "Void")] Void = 2,
        [Display(Name = "Refund")] Refund = 3,
        [Display(Name = "Reversal")] Reversal = 4,
        [Display(Name = "Fee Assessment")] FeeAssessment = 5,
        [Display(Name = "Settlement")] Settlement = 6,
        [Display(Name = "Chargeback")] Chargeback = 7,
        [Display(Name = "Chargeback Reversal")] ChargebackReversal = 8,
        [Display(Name = "Tender Reference")] TenderReference = 9,
        // A fact type the interpreter does not recognise: recorded for traceability, posts nothing.
        [Display(Name = "Unclassified")] Unclassified = 10
    }
}
