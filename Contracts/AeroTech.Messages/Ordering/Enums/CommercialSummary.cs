using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum CommercialSummary
    {
        [Display(Name = "Draft")]
        Draft = 1,

        [Display(Name = "Active")]
        Active = 2,

        [Display(Name = "Cancelled")]
        Cancelled = 3,

        [Display(Name = "Inactive")]
        Inactive = 4,

        [Display(Name = "Closed")]
        Closed = 5
    }
}
