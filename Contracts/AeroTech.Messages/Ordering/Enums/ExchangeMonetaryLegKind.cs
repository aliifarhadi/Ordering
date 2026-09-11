using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum ExchangeMonetaryLegKind
    {
        [Display(Name = "Collection")] Collection = 1,
        [Display(Name = "Refund Due")] RefundDue = 2,
        [Display(Name = "Residual")] Residual = 3
    }
}
