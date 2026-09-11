using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum AncillaryExchangeDisposition
    {
        [Display(Name = "Reassociate Existing")] ReassociateExisting = 1,
        [Display(Name = "Refund")] Refund = 2,
        [Display(Name = "Exchange To New EMD")] ExchangeToNewEmd = 3,
        [Display(Name = "Retain As Residual")] RetainAsResidual = 4,
        [Display(Name = "Cancel")] Cancel = 5,
        [Display(Name = "Manual Review")] ManualReview = 6
    }
}
