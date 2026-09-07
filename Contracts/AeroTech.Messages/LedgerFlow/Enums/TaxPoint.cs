using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum TaxPoint
    {
        [Display(Name = "At Issue")] AtIssue,
        [Display(Name = "At Payment")] AtPayment,
        [Display(Name = "At Uplift")] AtUplift,
        [Display(Name = "Other Approved Trigger")] OtherApprovedTrigger
    }
}
