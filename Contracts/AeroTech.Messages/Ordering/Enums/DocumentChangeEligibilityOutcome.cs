using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum DocumentChangeEligibilityOutcome
    {
        [Display(Name = "Revalidate")] Revalidate = 1,
        [Display(Name = "Reissue Required")] ReissueRequired = 2,
        [Display(Name = "Denied")] Denied = 3,
        [Display(Name = "Pending Evidence")] PendingEvidence = 4,
    }
}
