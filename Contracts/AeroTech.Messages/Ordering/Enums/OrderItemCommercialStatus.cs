using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderItemCommercialStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Replaced")] Replaced = 2,
        [Display(Name = "Cancelled")] Cancelled = 3,
    }
}
