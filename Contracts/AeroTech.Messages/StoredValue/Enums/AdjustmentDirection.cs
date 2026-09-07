using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum AdjustmentDirection
    {
        [Display(Name = "Credit")] Credit = 1,
        [Display(Name = "Debit")] Debit = 2
    }
}
