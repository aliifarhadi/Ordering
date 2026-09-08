using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingComponentType
    {
        [Display(Name = "Fare")] Fare = 1,
        [Display(Name = "Product Charge")] ProductCharge = 2,
        [Display(Name = "Tax")] Tax = 3,
        [Display(Name = "Carrier Surcharge")] CarrierSurcharge = 4,
        [Display(Name = "Fee")] Fee = 5,
        [Display(Name = "Discount")] Discount = 6,
        [Display(Name = "Markup")] Markup = 7,
        [Display(Name = "Penalty")] Penalty = 8,
        [Display(Name = "Commission")] Commission = 9,
        [Display(Name = "Adjustment")] Adjustment = 10,
        [Display(Name = "Other")] Other = 11,
    }
}
