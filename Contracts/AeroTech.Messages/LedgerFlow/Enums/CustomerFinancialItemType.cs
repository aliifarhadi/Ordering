using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum CustomerFinancialItemType
    {
        [Display(Name = "Receivable")] Receivable,
        [Display(Name = "Credit")] Credit
    }
}
