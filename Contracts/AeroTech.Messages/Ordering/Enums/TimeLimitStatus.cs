using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum TimeLimitStatus
    {
        [Display(Name = "Active")]
        Active = 1,

        [Display(Name = "Met")]
        Met = 2,

        [Display(Name = "Lapsed")]
        Lapsed = 3,

        [Display(Name = "Cancelled")]
        Cancelled = 4
    }
}
