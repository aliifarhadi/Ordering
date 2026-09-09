using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingSource
    {
        [Display(Name = "Offer Provider")] OfferProvider = 1,
        [Display(Name = "Pricing Engine")] PricingEngine = 2,
        [Display(Name = "Supplier")] Supplier = 3,
        [Display(Name = "Manual")] Manual = 4,
        [Display(Name = "Ordering Derived")] OrderingDerived = 5,
    }
}
