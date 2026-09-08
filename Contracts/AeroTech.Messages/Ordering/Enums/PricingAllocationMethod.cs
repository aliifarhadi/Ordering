using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum PricingAllocationMethod
    {
        [Display(Name = "Source Provided")] SourceProvided = 1,
        [Display(Name = "Direct Basis")] DirectBasis = 2,
        [Display(Name = "Exact Rule")] ExactRule = 3,
        [Display(Name = "Pro Rata")] ProRata = 4,
        [Display(Name = "Equal Split")] EqualSplit = 5,
        [Display(Name = "Weighted")] Weighted = 6,
        [Display(Name = "Manual")] Manual = 7,
    }
}
