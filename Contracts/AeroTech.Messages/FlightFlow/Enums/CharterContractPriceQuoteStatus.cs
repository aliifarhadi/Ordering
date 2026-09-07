using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum CharterContractPriceQuoteStatus
{
    [Display(Name = "Draft")] Draft =1,
    [Display(Name = "Running")] Running =2,
    [Display(Name = "Pause")] Pause =3
}