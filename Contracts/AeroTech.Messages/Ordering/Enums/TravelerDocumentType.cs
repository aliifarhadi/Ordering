using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum TravellerDocumentType
    {
        [Display(Name = "Unknown")]
        Unknown = 0,

        [Display(Name = "PASSPORT")]
        Passport = 1,

        [Display(Name = "IDENTITY CARD")]
        Identity = 2,

        [Display(Name = "VISA")]
        Visa = 3,

        [Display(Name = "REDRESS")]
        Redress = 4,

        [Display(Name = "KNOWN TRAVELER")]
        KnownTraveller = 5,
    }
}
