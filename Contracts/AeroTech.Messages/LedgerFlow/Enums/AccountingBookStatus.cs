using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountingBookStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Active")] Active = 2,
        [Display(Name = "Closed")] Closed = 3
    }
}
