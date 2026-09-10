using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ChangeDocumentOutcome
    {
        [Display(Name = "Not Attempted")] NotAttempted = 0,
        [Display(Name = "Revalidated")] Revalidated = 1,
        [Display(Name = "Reissue Required")] ReissueRequired = 2,
        [Display(Name = "Denied")] Denied = 3,
        [Display(Name = "Pending")] Pending = 4,
        [Display(Name = "Rejected")] Rejected = 5,
    }
}
