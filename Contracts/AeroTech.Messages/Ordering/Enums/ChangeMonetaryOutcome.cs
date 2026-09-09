using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ChangeMonetaryOutcome
    {
        [Display(Name = "Even")] Even = 1,
        [Display(Name = "Add Collect")] AddCollect = 2,
        [Display(Name = "Refund")] Refund = 3,
        [Display(Name = "Residual")] Residual = 4,
        [Display(Name = "Mixed")] Mixed = 5,
    }
}
