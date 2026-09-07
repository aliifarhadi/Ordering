using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightStatus
{
    [Display(Name = "Scheduled")] Scheduled = 1,
    [Display(Name = "Open")] Open,
    [Display(Name = "Closed")] Closed,
    [Display(Name = "Canceled")] Canceled,
    [Display(Name = "Boarding")] Boarding,
    [Display(Name = "Departed")] Departed,
    [Display(Name = "In Air/En Route")] AirEn,
    [Display(Name = "Diverted")] Diverted,
    [Display(Name = "Delayed")] Delayed,
    [Display(Name = "Landed")] Landed,
    [Display(Name = "Returned to Gate")] ReturnedGate,
    [Display(Name = "Completed")] Completed
}

