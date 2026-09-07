using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum EligibilityOutcome
    {
        [Display(Name = "Allowed")]
        Allowed = 1,

        [Display(Name = "Denied")]
        Denied = 2,

        [Display(Name = "Pending Evidence")]
        PendingEvidence = 3
    }
}
