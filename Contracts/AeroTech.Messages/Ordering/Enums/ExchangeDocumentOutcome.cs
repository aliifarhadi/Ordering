using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExchangeDocumentOutcome
    {
        [Display(Name = "Not Attempted")] NotAttempted = 0,
        [Display(Name = "Exchanged")] Exchanged = 1,
        [Display(Name = "Denied")] Denied = 2,
        [Display(Name = "Pending")] Pending = 3,
        [Display(Name = "Rejected")] Rejected = 4,
    }
}
