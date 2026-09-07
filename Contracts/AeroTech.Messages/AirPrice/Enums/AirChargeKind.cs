using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum AirChargeKind
{
    [Display(Name = "Tax")] Tax = 1,
    [Display(Name = "Surcharge")] Surcharge,
    [Display(Name = "Fee")] Fee
}