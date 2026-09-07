using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ContactRole
    {
        [Display(Name = "Primary")]
        Primary = 1,

        [Display(Name = "Emergency")]
        Emergency = 2,

        [Display(Name = "Agency")]
        Agency = 3,

        [Display(Name = "Traveler")]
        Traveler = 4,

        [Display(Name = "Other")]
        Other = 5
    }
}
