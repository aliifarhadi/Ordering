using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderPricingLineDirection
    {
        [Display(Name = "DEBIT")]
        Debit = 1,

        [Display(Name = "CREDIT")]
        Credit = 2,
    }
}
