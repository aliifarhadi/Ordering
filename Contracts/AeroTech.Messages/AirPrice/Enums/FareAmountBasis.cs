using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirPrice.Enums;

public enum FareAmountBasis
{
    [Display(Name = "OneWay")] OneWay = 1,
    [Display(Name = "RoundTrip")] RoundTrip,
    [Display(Name = "Half RoundTrip")] HalfRoundTrip,
    [Display(Name = "Total/Component")] TotalComponent,
}