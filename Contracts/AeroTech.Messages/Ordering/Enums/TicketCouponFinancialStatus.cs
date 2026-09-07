using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum TicketCouponFinancialStatus
    {
        [Display(Name = "Open")]
        Open = 1,

        [Display(Name = "Used")]
        Used = 2,

        [Display(Name = "Void")]
        Void = 3,

        [Display(Name = "Exchanged")]
        Exchanged = 4,

        [Display(Name = "Refunded")]
        Refunded = 5,

        [Display(Name = "Suspended")]
        Suspended = 6
    }
}
