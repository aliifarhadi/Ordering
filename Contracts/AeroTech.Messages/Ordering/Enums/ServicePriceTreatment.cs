using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ServicePriceTreatment
    {
        [Display(Name = "Separately Priced")] SeparatelyPriced = 1,
        [Display(Name = "Included")] Included = 2,
        [Display(Name = "Complimentary")] Complimentary = 3,
        [Display(Name = "Supplier Opaque")] SupplierOpaque = 4,
    }
}
