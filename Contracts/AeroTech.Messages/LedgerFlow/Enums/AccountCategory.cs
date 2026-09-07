using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AccountCategory
    {
        [Display(Name = "Asset")] Asset = 1,
        [Display(Name = "Liability")] Liability = 2,
        [Display(Name = "Equity")] Equity = 3,
        [Display(Name = "Revenue")] Revenue = 4,
        [Display(Name = "Expense")] Expense = 5
    }
}
