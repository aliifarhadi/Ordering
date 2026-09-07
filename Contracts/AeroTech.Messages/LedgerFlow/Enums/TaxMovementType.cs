using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum TaxMovementType
    {
        [Display(Name = "Charge")] Charge,
        [Display(Name = "Refund")] Refund,
        [Display(Name = "Remit")] Remit,
        [Display(Name = "Retain")] Retain,
        [Display(Name = "Correct")] Correct
    }
}
