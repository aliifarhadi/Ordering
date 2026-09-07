using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum VoidReason
    {
        [Display(Name = "Booking Error")] BookingError = 1,
        [Display(Name = "Duplicate")] Duplicate = 2,
        [Display(Name = "Agent Error")] AgentError = 3,
        [Display(Name = "Customer Request")] CustomerRequest = 4,
        [Display(Name = "Fare Error")] FareError = 5,
        [Display(Name = "Schedule Change")] ScheduleChange = 6,
        [Display(Name = "Test")] Test = 7,
        [Display(Name = "Other")] Other = 8
    }
}
