using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderItemPaymentStatus
    {
        [Display(Name = "Unpaid")] Unpaid = 1,
        [Display(Name = "Paid")] Paid = 2,
        [Display(Name = "Refunded")] Refunded = 3,
    }
    }
