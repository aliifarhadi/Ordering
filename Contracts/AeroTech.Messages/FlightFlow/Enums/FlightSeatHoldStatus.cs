using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightSeatHoldStatus
{
    [Display(Name = "Held")] Held = 1,

    [Display(Name = "Released")] Released = 2,

    [Display(Name = "Expired")] Expired = 3,

    [Display(Name = "Confirmed")] Confirmed = 4,

    [Display(Name = "Canceled")] Cancelled = 5,
}
