using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum JournalType
    {
        [Display(Name = "Standard")] Standard = 1,
        [Display(Name = "Reversal")] Reversal = 2,
        [Display(Name = "Adjustment")] Adjustment = 3
    }
}
