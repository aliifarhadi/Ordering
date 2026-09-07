using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum AirFareType
{
    [Display(Name = "Public")] Public = 1,
    [Display(Name = "Private")] Private = 2,
    [Display(Name = "Negotiated")] Negotiated = 3,
    [Display(Name = "Charter")] Charter = 4
}

