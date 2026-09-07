using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum AirChargeState
{
    [Display(Name = "Draft")] Draft = 1,
    [Display(Name = "Released")] Released,
    [Display(Name = "Suspended")] Suspended
}
