using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightDisruptionType
{

    [Display(Name = "Schedule Change")] ScheduleChange = 1,
    [Display(Name = "Status Change")] StatusChange,
    [Display(Name = "Aircraft Change")] AirCraftChange
}