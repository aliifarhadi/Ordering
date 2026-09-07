using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum BlackoutType

{
    [Display(Name = "Travel")] Travel = 1,
    [Display(Name = "Ticketing")] Ticketing = 2
}