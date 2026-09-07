using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FulfillmentReservationStatus
    {
        [Display(Name = "Pending")]
        Pending = 1,

        [Display(Name = "Waitlisted")]
        Waitlisted = 2,

        [Display(Name = "Confirmed")]
        Confirmed = 3,

        [Display(Name = "Rejected")]
        Rejected = 4,

        [Display(Name = "Cancellation Pending")]
        CancellationPending = 5,

        [Display(Name = "Released")]
        Released = 6,

        [Display(Name = "Unknown")]
        Unknown = 7,

        [Display(Name = "Expired")]
        Expired = 8,

        [Display(Name = "Mixed")]
        Mixed = 9
    }
}
