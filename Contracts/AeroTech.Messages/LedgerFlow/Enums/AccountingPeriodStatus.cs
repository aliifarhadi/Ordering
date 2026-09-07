using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountingPeriodStatus
    {
        [Display(Name = "Future")] Future = 1,
        [Display(Name = "Open")] Open = 2,
        [Display(Name = "Soft Closed")] SoftClosed = 3,
        [Display(Name = "Closed")] Closed = 4,
        [Display(Name = "Locked")] Locked = 5
    }
}
