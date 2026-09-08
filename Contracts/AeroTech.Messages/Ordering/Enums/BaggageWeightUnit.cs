using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum BaggageWeightUnit
    {
        [Display(Name = "Kilograms")] Kg = 1,
        [Display(Name = "Pounds")] Lbs = 2,
    }
}
