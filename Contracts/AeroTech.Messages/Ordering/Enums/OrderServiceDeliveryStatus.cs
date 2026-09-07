using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderServiceDeliveryStatus
    {
        [Display(Name = "Not Ready")] NotReady = 1,
        [Display(Name = "Ready To Deliver")] ReadyToDeliver = 2,
        [Display(Name = "Delivered")] Delivered = 3,
        [Display(Name = "Consumed")] Consumed = 4,
        [Display(Name = "No Show")] NoShow = 5,
        [Display(Name = "Unused")] Unused = 6,
        [Display(Name = "Failed")] Failed = 7
    }
}
