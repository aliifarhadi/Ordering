using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum FlightApplicationIncludeType
{
    [Display(Name = "Include")] Include = 1,
    [Display(Name = "Exclude")] Exclude
}