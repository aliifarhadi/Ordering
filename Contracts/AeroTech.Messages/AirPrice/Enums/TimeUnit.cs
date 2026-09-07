using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum TimeUnit
{
    [Display(Name = "Minute(s)")]Minutes=1,
    [Display(Name = "Hour(s)")]Hours,
    [Display(Name = "Day(s)")]Days,
    [Display(Name = "Week(s)")]Weeks,
    [Display(Name = "Month(s)")]Months,
    [Display(Name = "Year(s)")] Years,
}


