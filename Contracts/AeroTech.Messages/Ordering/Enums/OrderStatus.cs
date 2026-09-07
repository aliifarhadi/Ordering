using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderStatus
    {
        [Display(Name = "None")]
        None = 0,

        [Display(Name = "Created")]
        Created = 1,

        [Display(Name = "Confirmed")]
        Confirmed = 2,

        [Display(Name = "Ticketed")]
        Ticketed = 3,

        [Display(Name = "Reserve Failed")]
        ReserveFailed = 4,

        [Display(Name = "Expired")]
        Expired = 6, 

        [Display(Name = "Cancelled")]
        Cancelled = 7,

        [Display(Name = "Payment Failed")]
        PaymentFailed = 8,

        [Display(Name = "Pending Ticketed")]
        Ticketing = 9,

        [Display(Name = "Pending Payment")]
        Paying = 10,

        [Display(Name = "Refunded")]
        Refunded = 11,

        [Display(Name = "Reservation Unconfirmed")]
        ReservationUnconfirmed = 12,

        [Display(Name = "Paid")]
        Paid = 13,

        [Display(Name = "Payment Unconfirmed")]
        PaymentUnconfirmed = 14,

        [Display(Name = "Ticketing Failed")]
        TicketingFailed = 15,

        [Display(Name = "Ticketing Unconfirmed")]
        TicketingUnconfirmed = 16,

        [Display(Name = "Void Unconfirmed")]
        VoidUnconfirmed = 17,

        [Display(Name = "Cancel Unconfirmed")]
        CancelUnconfirmed = 18
    }

}
