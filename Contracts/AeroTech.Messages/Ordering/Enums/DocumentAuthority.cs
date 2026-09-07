using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum DocumentAuthority
    {
        [Display(Name = "Local")]
        Local = 1,

        [Display(Name = "External")]
        External = 2
    }
}
