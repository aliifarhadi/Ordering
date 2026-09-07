using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderFulfillmentStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "In Progress")] InProgress = 2,
        [Display(Name = "Confirmed")] Confirmed = 3,
        [Display(Name = "Failed")] Failed = 4,
        [Display(Name = "Manual Action Required")] ManualActionRequired = 5,
        [Display(Name = "Cancelled")] Cancelled = 6,
        [Display(Name = "Reversed")] Reversed = 7

    }
}
