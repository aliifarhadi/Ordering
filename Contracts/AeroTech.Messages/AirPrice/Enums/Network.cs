using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum Network
{
    [Display(Name = "Both")]
    Both = 1,

    [Display(Name = "Dom")]
    Domestic,

    [Display(Name = "Intl")]
    International
}