using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderProviderType
    {
        [Display(Name = "Airline")] Airline = 1,
        [Display(Name = "Supplier")] Supplier = 2,
        [Display(Name = "Aggregator")] Aggregator = 3,
        [Display(Name = "Internal Provider")] InternalProvider = 4
    }
}
