using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum TaxRemittanceRunStatus
    {
        [Display(Name = "Draft")] Draft,
        [Display(Name = "Calculated")] Calculated,
        [Display(Name = "Approved")] Approved,
        [Display(Name = "Payment Requested")] PaymentRequested,
        [Display(Name = "Paid")] Paid,
        [Display(Name = "Confirmed")] Confirmed
    }
}
