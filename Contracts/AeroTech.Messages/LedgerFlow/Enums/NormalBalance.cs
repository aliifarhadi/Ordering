using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum NormalBalance
    {
        [Display(Name = "Debit")] Debit = 1,
        [Display(Name = "Credit")] Credit = 2
    }
}
