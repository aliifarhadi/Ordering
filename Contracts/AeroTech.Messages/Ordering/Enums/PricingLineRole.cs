using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingLineRole
    {
        [Display(Name = "Original")] Original = 1,
        [Display(Name = "Reversal")] Reversal = 2,
        [Display(Name = "Adjustment")] Adjustment = 3,
        [Display(Name = "Transfer")] Transfer = 4,
    }
}
