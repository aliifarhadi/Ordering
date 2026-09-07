using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum TransportationType
{
    [Display(Name = "Air")]
    Air =1,

    [Display(Name = "Surface")]
    Surface
}
