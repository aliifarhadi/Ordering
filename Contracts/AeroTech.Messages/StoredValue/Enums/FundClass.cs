using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum FundClass
    {
        [Display(Name = "Customer Funded")] CustomerFunded = 1,
        [Display(Name = "Refund Credit")] RefundCredit = 2,
        [Display(Name = "Goodwill Credit")] GoodwillCredit = 3,
        [Display(Name = "Promotional Credit")] PromotionalCredit = 4,
        [Display(Name = "Compensation Credit")] CompensationCredit = 5,
        [Display(Name = "Agency Deposit")] AgencyDeposit = 6,
        [Display(Name = "Other Restricted Credit")] OtherRestrictedCredit = 7
    }
}
