using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum CashReceiptStatus
    {
        [Display(Name = "Recorded")] Recorded,
        [Display(Name = "Available")] Available,
        [Display(Name = "Partially Allocated")] PartiallyAllocated,
        [Display(Name = "Fully Allocated")] FullyAllocated,
        [Display(Name = "Refunded")] Refunded,
        [Display(Name = "Voided")] Voided
    }
}
