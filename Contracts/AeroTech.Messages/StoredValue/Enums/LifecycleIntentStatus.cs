using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum LifecycleIntentStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "None")] Applying = 2,
        [Display(Name = "Completed")] Completed = 3,
        [Display(Name = "None")] Cancelled = 4
    }
}
