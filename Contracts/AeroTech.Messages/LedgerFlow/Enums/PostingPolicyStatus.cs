using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    public enum PostingPolicyStatus
    {
        [Display(Name = "Draft")] Draft = 1,
        [Display(Name = "Tested")] Tested = 2,
        [Display(Name = "Pending Approval")] PendingApproval = 3,
        [Display(Name = "Published")] Published = 4,
        [Display(Name = "Retired")] Retired = 5
    }
}
