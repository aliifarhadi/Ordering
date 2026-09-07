using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum VoluntaryRefundsStrictnessLevel
{
    [Display(Name = "Strict")] Strict = 1,
    [Display(Name = "Moderate")] Moderate,
    [Display(Name = "Lenient")] Lenient
}