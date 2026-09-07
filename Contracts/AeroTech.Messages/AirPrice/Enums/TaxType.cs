using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum TaxType
{
    [Display(Name = "Tax")] Tax = 1,
    [Display(Name = "Surcharge")] Surcharge,
    [Display(Name = "ServiceFee")] ServiceFee
}