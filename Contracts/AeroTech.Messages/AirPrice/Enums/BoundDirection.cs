using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum BoundDirection
{
    [Display(Name = "Any")] Any = 1,
    [Display(Name = "Outbound")] Outbound ,
    [Display(Name = "Inbound")] Inbound,
   
}