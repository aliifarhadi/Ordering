using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum PostingSide
    {
        [Display(Name = "Debit")] Debit = 1,
        [Display(Name = "Credit")] Credit = 2
    }
}
