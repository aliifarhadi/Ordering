using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FareCombinationMethod
    {
        [Display(Name = "Unspecified")] Unspecified = 1,
        [Display(Name = "Filed Fare")] FiledFare = 2,
        [Display(Name = "Local Combination")] LocalCombination = 3,
        [Display(Name = "Dynamic")] Dynamic = 4,
        [Display(Name = "Provider Defined")] ProviderDefined = 5,
    }
}
