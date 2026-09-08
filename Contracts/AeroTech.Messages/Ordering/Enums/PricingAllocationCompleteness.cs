using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingAllocationCompleteness
    {
        [Display(Name = "Complete")] Complete = 1,
        [Display(Name = "Partial")] Partial = 2,
        [Display(Name = "Unavailable")] Unavailable = 3,
    }
}
