using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AirFareConstructionType
    {
        [Display(Name = "Unspecified")] Unspecified = 1,
        [Display(Name = "One Way")] OneWay = 2,
        [Display(Name = "Round Trip")] RoundTrip = 3,
        [Display(Name = "Round Trip From One Ways")] RoundTripFromOneWays = 4,
        [Display(Name = "Open Jaw")] OpenJaw = 5,
        [Display(Name = "Circle Trip")] CircleTrip = 6,
        [Display(Name = "Mixed")] Mixed = 7,
        [Display(Name = "Provider Defined")] ProviderDefined = 8,
    }
}
