using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum NoShowTimeAxis
{
    [Display(Name = "Departure")] Departure=1,
    [Display(Name = "After Departure")] AfterDeparture
}