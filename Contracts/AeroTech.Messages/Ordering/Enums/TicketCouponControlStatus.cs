using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum TicketCouponControlStatus
    {
        [Display(Name = "Local")]
        Local = 1,

        [Display(Name = "External")]
        External = 2,

        [Display(Name = "Release Pending")]
        ReleasePending = 3,

        [Display(Name = "Unknown")]
        Unknown = 4
    }
}
