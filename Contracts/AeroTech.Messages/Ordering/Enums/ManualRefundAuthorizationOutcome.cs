using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ManualRefundAuthorizationOutcome
    {
        [Display(Name = "Approved")] Approved = 1,
        [Display(Name = "Denied")] Denied = 2,
        [Display(Name = "Unavailable")] Unavailable = 3,
    }
}
