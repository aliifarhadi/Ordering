using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingBasisType
    {
        [Display(Name = "Order")] Order = 1,
        [Display(Name = "Order Item")] OrderItem = 2,
        [Display(Name = "Order Service")] OrderService = 3,
        [Display(Name = "Journey")] Journey = 4,
        [Display(Name = "Segment")] Segment = 5,
        [Display(Name = "Pricing Unit")] PricingUnit = 6,
        [Display(Name = "Fare Component")] FareComponent = 7,
        [Display(Name = "External Charge")] ExternalCharge = 8,
    }
}
