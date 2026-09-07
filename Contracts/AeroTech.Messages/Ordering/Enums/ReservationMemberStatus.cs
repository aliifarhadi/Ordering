using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ReservationMemberStatus
    {
        [Display(Name = "Pending")]
        Pending = 1,

        [Display(Name = "Waitlisted")]
        Waitlisted = 2,

        [Display(Name = "Confirmed")]
        Confirmed = 3,

        [Display(Name = "Rejected")]
        Rejected = 4,

        [Display(Name = "Released")]
        Released = 5,

        [Display(Name = "Expired")]
        Expired = 6,

        [Display(Name = "Unknown")]
        Unknown = 7
    }
}
