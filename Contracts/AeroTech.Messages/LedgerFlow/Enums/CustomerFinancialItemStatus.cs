using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum CustomerFinancialItemStatus
    {
        [Display(Name = "Open")] Open,
        [Display(Name = "Partially Allocated")] PartiallyAllocated,
        [Display(Name = "Settled")] Settled,
        [Display(Name = "Written Off")] WrittenOff
    }
}
