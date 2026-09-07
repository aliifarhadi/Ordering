using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountingExceptionStatus
    {
        [Display(Name = "Open")] Open = 1,
        [Display(Name = "Retrying")] Retrying = 2,
        [Display(Name = "Resolved")] Resolved = 3,
        [Display(Name = "Dismissed")] Dismissed = 4
    }
}
