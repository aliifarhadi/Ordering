using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum OpenItemApplicationType
    {
        [Display(Name = "Payment")] Payment,
        [Display(Name = "Credit")] Credit,
        [Display(Name = "Write-Off")] WriteOff,
        [Display(Name = "Reversal")] Reversal
    }
}
