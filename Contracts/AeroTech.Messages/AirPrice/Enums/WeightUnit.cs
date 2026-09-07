using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum WeightUnit
{
    [Display(Name = "Kilograms")] Kg = 1,
    [Display(Name = "Pounds ")] Lbs
}
