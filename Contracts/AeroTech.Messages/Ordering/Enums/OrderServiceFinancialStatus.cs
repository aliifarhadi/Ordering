using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Ordering.Enums
{
    public enum OrderServiceFinancialStatus
    {
        [Display(Name = "Not Ready")] NotPriced = 1,
        [Display(Name = "Priced")] Priced = 2,
        [Display(Name = "Paid")] Paid = 3,
        [Display(Name = "Partially Paid")] PartiallyPaid = 4,
        [Display(Name = "Refunded")] Refunded = 5,
        [Display(Name = "Partially Refunded")] PartiallyRefunded = 6,
        [Display(Name = "Non Refundable")] NonRefundable = 7,
        [Display(Name = "Revenue Recognized")] RevenueRecognized = 8
    }
}
