using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;


public enum SeasonType
{
    [Display(Name = "Seasonal")] Seasonal = 1,
    [Display(Name = "Promotional")] Promotional,

}