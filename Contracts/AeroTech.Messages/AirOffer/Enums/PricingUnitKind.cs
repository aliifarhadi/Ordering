using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.AirOffer.Enums
{
    public enum PricingUnitKind
    {
        [Display(Name = "OneWay")] OneWay = 1,
        [Display(Name = "RoundTripFromOneWays")] RoundTripFromOneWays = 2,
        [Display(Name = "RoundTripFare")] RoundTripFare = 3
    }
}
