using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum RefundabilityRule
    {

        [Display(Description = "FULL IF UNUSED")]
        FullIfAllUnused = 1,

        [Display(Description = "PRORATE")]
        ProRata  = 2,

        [Display(Description = "NON REFUNDABLE")]
        NonRefundable = 3,

        [Display(Description = "REFUNDABLE")]
        Refundable = 4,
 

        [Display(Description = "CONDITIONAL")]
        Conditional = 5,
    }
}
