using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum CharterContractStatus
{
    [Display(Name = "Draft")] Draft =1,
    [Display(Name = "Release")] Release =2
}