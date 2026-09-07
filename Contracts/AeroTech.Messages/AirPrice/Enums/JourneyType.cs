using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum JourneyType
{
    [Display(Name = "OneWay")] OneWay = 1,

    [Display(Name = "RoundTrip")] RoundTrip,

    [Display(Name = "Multi-city")] Circle,

    [Display(Name = "Open-jaw")] OpenJaw
}
