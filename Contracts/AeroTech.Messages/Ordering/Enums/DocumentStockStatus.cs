using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum DocumentStockStatus
    {
        [Display(Name = "Active")]
        Active = 1,

        [Display(Name = "Suspended")]
        Suspended = 2,

        [Display(Name = "Exhausted")]
        Exhausted = 3,

        [Display(Name = "Closed")]
        Closed = 4
    }
}
