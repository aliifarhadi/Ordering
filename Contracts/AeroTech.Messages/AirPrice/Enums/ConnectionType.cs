using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum ConnectionType
{
    [Display(Name = "Any")]Any=1, 
    [Display(Name = "Direct")]Direct,
    [Display(Name = "NonDirect")] NonDirect
}