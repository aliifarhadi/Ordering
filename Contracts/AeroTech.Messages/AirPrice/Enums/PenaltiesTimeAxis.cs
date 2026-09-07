using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum PenaltiesTimeAxis
{
    [Display(Name = "Anytime")]Anytime=1,
    [Display(Name = "Before Departure")]BeforeDeparture,
    [Display(Name = "After Departure")] AfterDeparture

}