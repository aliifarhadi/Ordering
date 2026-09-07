using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum ReceivableStatus
    {
        [Display(Name = "Open")] Open,
        [Display(Name = "Partially Settled")] PartiallySettled,
        [Display(Name = "Settled")] Settled,
        [Display(Name = "Written Off")] WrittenOff,
        [Display(Name = "Cancelled")] Cancelled
    }
}
