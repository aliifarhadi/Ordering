using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum JourneyType
    {
        [Display(Name = "OneWay")] OneWay = 1,
        [Display(Name = "RoundTrip")] RoundTrip = 2,
        [Display(Name = "Multi-city")] Circle = 3,
        [Display(Name = "Open-jaw")] OpenJaw = 4,
    }
}
