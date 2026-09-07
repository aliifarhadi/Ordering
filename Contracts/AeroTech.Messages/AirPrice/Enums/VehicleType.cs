using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum VehicleType
{
    [Display(Name = "AirCraft")]
    AirCraft = 1,

    [Display(Name = "Car")]
    Car 
}
