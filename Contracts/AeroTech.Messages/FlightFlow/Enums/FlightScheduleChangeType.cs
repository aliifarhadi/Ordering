using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightScheduleChangeType
{
    [Display(Name = "MinorDelay")] MinorDelay=1,
    [Display(Name = "MajorDelay")] MajorDelay,
    [Display(Name = "MinorAdvancement")] MinorAdvancement,
    [Display(Name = "MajorAdvancement")] MajorAdvancement,
    [Display(Name = "ReSchedule")] ReSchedule,

}