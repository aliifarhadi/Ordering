using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum StopType
{
    [Display(Name = "Any")]Any=1,
    [Display(Name = "Stop")]Stop ,
    [Display(Name = "Stopover")]Stopover,
    [Display(Name = "Connection Intermediate Stop")]ConnectionIntermediateStop,
    [Display(Name = "Forced Stops")] ForcedStops
}