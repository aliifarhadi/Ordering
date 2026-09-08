using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum FarePricingUnitType
    {
        [Display(Name = "Unspecified")] Unspecified = 1,
        [Display(Name = "One Way")] OneWay = 2,
        [Display(Name = "Round Trip")] RoundTrip = 3,
        [Display(Name = "Open Jaw")] OpenJaw = 4,
        [Display(Name = "Circle Trip")] CircleTrip = 5,
        [Display(Name = "Other")] Other = 6,
    }
}
