using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ProviderInteractionStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Succeeded")] Succeeded = 2,
        [Display(Name = "Failed")] Failed = 3,
        [Display(Name = "Timed Out")] TimedOut = 4
    }
}
