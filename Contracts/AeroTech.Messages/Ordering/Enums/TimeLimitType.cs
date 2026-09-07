using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum TimeLimitType
    {
        [Display(Name = "Ticketing")]
        Ticketing = 1,

        [Display(Name = "Payment")]
        Payment = 2,

        [Display(Name = "Reservation")]
        Reservation = 3,

        [Display(Name = "Name Entry")]
        NameEntry = 4
    }
}
