using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightCapacityAllocationType
{
    [Display(Name = "Public")]Public=1,
    [Display(Name = "Private Allocated")]PrivateAllocated=2
    
}