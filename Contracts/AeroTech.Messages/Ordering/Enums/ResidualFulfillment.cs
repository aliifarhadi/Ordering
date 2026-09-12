using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ResidualFulfillment
    {
        [Display(Name = "External Value")] ExternalValue = 1,
        [Display(Name = "Document Coupled")] DocumentCoupled = 2
    }
}
