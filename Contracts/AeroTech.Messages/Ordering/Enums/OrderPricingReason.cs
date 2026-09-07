using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderPricingReason 
    {

        [Display(Description = "Initial Sale")]
        InitialSale = 1,

        [Display(Description = "Exchange")]
        Exchange = 2,

        [Display(Description = "Refund")]
        Refund = 3,

        [Display(Description = "Upgrade")]
        Upgrade = 4,

        [Display(Description = "Penalty Collection")]
        Penalty = 5,

        [Display(Description = "Residual Issuance")]
        ResidualIssuance = 6,

        [Display(Description = "Void")]
        Void = 7,

        [Display(Description = "Cancel")]
        Cancel = 8,
    }
}
