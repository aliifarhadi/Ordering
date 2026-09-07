using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum OperationalAccount
    {
       [Display(Name = "Customer Available")] CustomerAvailable = 1,
       [Display(Name = "Customer Held")] CustomerHeld = 2,
       [Display(Name = "Funding Clearing")] FundingClearing = 3,
       [Display(Name = "Spending Clearing")] SpendingClearing = 4,
       [Display(Name = "Refund Restoration Clearing")] RefundRestorationClearing = 5,
       [Display(Name = "Expired Value")] ExpiredValue = 6,
       [Display(Name = "Adjustment Control")] AdjustmentControl = 7,
       [Display(Name = "Credit Reversal Clearing")] CreditReversalClearing = 8
    }
}
