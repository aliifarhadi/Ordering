using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum SeasonLabel
{
    [Display(Name = "All")]All = 1,
    [Display(Name = "Low")] Low,
    [Display(Name = "Shoulder")] Shoulder,
    [Display(Name = "High")]High

}