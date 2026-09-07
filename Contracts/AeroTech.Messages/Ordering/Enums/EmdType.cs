using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum EmdType
    {
        [Display(Name = "EMD-A")]
        EMDA = 1,

        [Display(Name = "EMD-S")]
        EMDS = 2,

        [Display(Name = "EMD-R")]
        EMDR = 3
    }
}
