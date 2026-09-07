using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum AdjustmentRequestStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Submitted")] Submitted = 2,
        [Display(Name = "Approved")] Approved = 3,
        [Display(Name = "Rejected")] Rejected = 4,
        [Display(Name = "Posted")] Posted = 5
    }
}
