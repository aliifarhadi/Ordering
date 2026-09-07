using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum CustomerFinancialAccountStatus
    {
        [Display(Name = "Active")] Active,
        [Display(Name = "Suspended")] Suspended,
        [Display(Name = "Closed")] Closed
    }
}
