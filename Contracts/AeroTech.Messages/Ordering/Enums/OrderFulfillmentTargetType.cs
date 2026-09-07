using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderFulfillmentTargetType
    {
        [Display(Name = "Order")] Order = 1,
        [Display(Name = "Order Item")] OrderItem = 2,
        [Display(Name = "Order Service")] OrderService = 3,
        [Display(Name = "Ticket")] Ticket = 4,
        [Display(Name = "Coupon")] Coupon = 5,
        [Display(Name = "Payment")] Payment = 6,         
        [Display(Name = "Refund")] Refund = 7,    
        [Display(Name = "ProviderReservation")] ProviderReservation = 8,  
    }
}