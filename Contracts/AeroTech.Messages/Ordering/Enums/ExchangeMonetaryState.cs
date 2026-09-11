using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExchangeMonetaryState
    {
        [Display(Name = "Not Required")] NotRequired = 1,
        [Display(Name = "Required")] Required = 2,
        [Display(Name = "Pending")] Pending = 3,
        [Display(Name = "Settled")] Settled = 4,
        [Display(Name = "Rejected")] Rejected = 5,
        [Display(Name = "Released")] Released = 6
    }
}
