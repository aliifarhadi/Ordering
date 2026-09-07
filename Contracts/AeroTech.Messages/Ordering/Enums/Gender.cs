using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum Gender
    {
        [Display(Name = "Ms.", Description = "FEMALE")]
        Female = 1,

        [Display(Name = "Mr.", Description = "MALE")]
        Male = 2,

        [Display(Name = "", Description = "UNSPECIFIED")]
        UnSpecified = 3,

        [Display(Name = "UN.", Description = "UNDISCLOSED")]
        UnDisclosed = 4,
    }
}
