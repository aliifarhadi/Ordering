using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ElectronicTicketStatus
    {
        [Display(Name = "Issued")]
        Issued = 1,

        [Display(Name = "Partially Used")]
        PartiallyUsed = 2,

        [Display(Name = "Used")]
        Used = 3,

        [Display(Name = "Voided")]
        Voided = 4,

        [Display(Name = "Exchanged")]
        Exchanged = 5,

        [Display(Name = "Refunded")]
        Refunded = 6,

        [Display(Name = "Suspended")]
        Suspended = 7
    }
}
