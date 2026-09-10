using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExchangeCouponDisposition
    {
        [Display(Name = "Replaced")] Replaced = 1,
        [Display(Name = "Continued")] Continued = 2,
    }
}
