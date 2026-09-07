using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderPricingLineScope
    {
        [Display(Name = "Order")] Order = 1,
        [Display(Name = "Order Item")] OrderItem = 2,
        [Display(Name = "Order Service")] OrderService = 3,
        [Display(Name = "Traveller Journey")] TravellerJourney = 4,
        [Display(Name = "Ticket")] Ticket = 5,
        [Display(Name = "Coupon")] Coupon = 6,
        [Display(Name = "Payment ")] Payment  = 7
    }
}
