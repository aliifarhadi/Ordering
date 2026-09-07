using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AgeRange
    {
        [Display(Name = "N/A")]
        Unknown = 0,

        [Display(Name ="ADT")]
        Adult = 1,

        [Display(Name = "CHD")]
        Child = 2,

        [Display(Name = "INF")]
        Infant = 3
    }
}
