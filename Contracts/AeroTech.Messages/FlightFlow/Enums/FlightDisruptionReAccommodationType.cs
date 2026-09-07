using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightDisruptionRebookOptionType
{
    [Display(Name = "Force Rebook")] ForceRebook = 1,
    [Display(Name = "Force Cancel")] ForceCancel,
    [Display(Name = "Escalate")] Escalate
}

public enum FlightDisruptionActionType
{
    [Display(Name = "No Action")] NoAction = 1,
    [Display(Name = "Rebook")] Rebook
}