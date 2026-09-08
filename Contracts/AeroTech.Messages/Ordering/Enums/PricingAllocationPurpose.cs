using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingAllocationPurpose
    {
        [Display(Name = "Commercial Value")] CommercialValue = 1,
        [Display(Name = "Servicing")] Servicing = 2,
        [Display(Name = "Accounting")] Accounting = 3,
        [Display(Name = "Settlement")] Settlement = 4,
        [Display(Name = "Reporting")] Reporting = 5,
    }
}
