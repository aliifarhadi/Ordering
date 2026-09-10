using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum DocumentExchangeEligibilityOutcome
    {
        [Display(Name = "Eligible")] Eligible = 1,
        [Display(Name = "Denied")] Denied = 2,
        [Display(Name = "Pending Evidence")] PendingEvidence = 3,
    }
}
