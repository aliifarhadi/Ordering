using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum CommercialTermState
    {
        [Display(Name = "Unknown")] Unknown = 1,
        [Display(Name = "Prohibited")] Prohibited = 2,
        [Display(Name = "Permitted")] Permitted = 3,
        [Display(Name = "Conditional")] Conditional = 4,
    }
}
