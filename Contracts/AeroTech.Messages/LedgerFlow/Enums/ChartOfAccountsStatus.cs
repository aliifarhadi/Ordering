using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum ChartOfAccountsStatus
    {
        [Display(Name = "Active")] Active,
        [Display(Name = "Inactive")] Inactive
    }
}
